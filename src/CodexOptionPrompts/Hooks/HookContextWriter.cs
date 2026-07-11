using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CodexOptionPrompts.Hooks {
    public static class HookContextWriter {
        public const string RoutingPolicy =
            "Mandatory user-question routing policy: Any time you would ask the user a question or request a response, clarification, confirmation, approval, missing detail, preference, or selection, call the ask_options MCP tool from the codex_option_prompts server immediately. Do not first write the question or choices in assistant prose. This includes both enumerable choices and genuinely open-ended questions. For an open-ended question, send one question with options: [], allowOther: true, and a useful otherLabel. Put all question and option text directly in the MCP JSON arguments. Never use PowerShell, a shell command, or a temporary file to transport question or option text, and do not read the option-prompts SKILL.md through the shell before calling the tool. Let the task determine the number of questions and options and whether each question is single- or multi-select. Do not call the tool when no user response is needed. If ask_options is unavailable or returns an MCP error, ask one concise plain-text question as the fallback.";

        public const string CompletionPolicy =
            "Mandatory completed-task follow-up policy: Whenever the current user-requested task is genuinely complete, use the next-step-options skill before ending the turn. Generate exactly 2 or 3 relevant next-task choices and call ask_options from codex_option_prompts so the user can click one. Do not merely list the choices in assistant prose. Do not run this workflow for incomplete work, blockers, greetings, status-only replies, or when the user explicitly opted out of follow-up suggestions.";

        public const string CompletionContinuationReason =
            "Before ending this turn, check whether the user's requested task is genuinely complete. If required work remains, continue it. If the task is complete and clickable follow-up choices have not already been handled for this completion, use $next-step-options now: create exactly 2 or 3 relevant next tasks and call codex_option_prompts.ask_options. Do not merely list them in prose. If the follow-up tool was already handled, the task is blocked or incomplete, this is only a greeting/status reply, or the user explicitly opted out, finish without another follow-up prompt.";

        public static void Write(string hookEvent, TextWriter output) {
            Write(hookEvent, new StringReader("{}"), output);
        }

        public static void Write(string hookEvent, TextReader input, TextWriter output) {
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");
            string canonicalEvent = CanonicalEvent(hookEvent);
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            if (canonicalEvent == "Stop") {
                IDictionary<string, object> response = new Dictionary<string, object>();
                if (!StopHookActive(input, serializer)) {
                    response["decision"] = "block";
                    response["reason"] = CompletionContinuationReason;
                }
                output.WriteLine(serializer.Serialize(response));
                return;
            }

            IDictionary<string, object> hookSpecificOutput = new Dictionary<string, object>();
            hookSpecificOutput["hookEventName"] = canonicalEvent;
            hookSpecificOutput["additionalContext"] = RoutingPolicy + " " + CompletionPolicy;

            IDictionary<string, object> contextResponse = new Dictionary<string, object>();
            contextResponse["hookSpecificOutput"] = hookSpecificOutput;
            output.WriteLine(serializer.Serialize(contextResponse));
        }

        public static string CanonicalEvent(string hookEvent) {
            if (String.Equals(hookEvent, "session-start", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(hookEvent, "SessionStart", StringComparison.OrdinalIgnoreCase)) {
                return "SessionStart";
            }
            if (String.Equals(hookEvent, "user-prompt-submit", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(hookEvent, "UserPromptSubmit", StringComparison.OrdinalIgnoreCase)) {
                return "UserPromptSubmit";
            }
            if (String.Equals(hookEvent, "stop", StringComparison.OrdinalIgnoreCase)) return "Stop";
            throw new ArgumentException("Unsupported hook event: " + hookEvent, "hookEvent");
        }

        private static bool StopHookActive(TextReader input, JavaScriptSerializer serializer) {
            string json = input.ReadToEnd();
            if (String.IsNullOrWhiteSpace(json)) return false;
            try {
                IDictionary<string, object> value = serializer.DeserializeObject(json) as IDictionary<string, object>;
                object active;
                return value != null && value.TryGetValue("stop_hook_active", out active) &&
                    active is bool && (bool)active;
            } catch (ArgumentException) { return false; }
        }
    }
}
