using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using CodexCue.Settings;

namespace CodexCue.Hooks {
    public static class HookContextWriter {
        public const string RoutingPolicy =
            "Mandatory user-question routing policy: Any time you would ask the user a question or request a response, clarification, confirmation, approval, missing detail, preference, or selection, call the ask_options MCP tool from the codex_cue server immediately. Do not first write the question or choices in assistant prose. This includes both enumerable choices and genuinely open-ended questions. For an open-ended question, send one question with options: [], allowOther: true, and a useful otherLabel. Put all question and option text directly in the MCP JSON arguments. Never use PowerShell, a shell command, or a temporary file to transport question or option text, and do not read the cue-prompts SKILL.md through the shell before calling the tool. Let the task determine the number of questions and options and whether each question is single- or multi-select. Do not call the tool when no user response is needed. If ask_options is unavailable or returns an MCP error, ask one concise plain-text question as the fallback.";

        public static void Write(string hookEvent, TextWriter output) {
            Write(hookEvent, new StringReader("{}"), output);
        }

        public static void Write(string hookEvent, TextReader input, TextWriter output) {
            Write(hookEvent, input, output, CueSettingsStore.Current());
        }

        public static void Write(string hookEvent, TextReader input, TextWriter output, CueSettingsStore settingsStore) {
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");
            if (settingsStore == null) throw new ArgumentNullException("settingsStore");
            string canonicalEvent = CanonicalEvent(hookEvent);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            CueSettings settings = settingsStore.Load();

            if (canonicalEvent == "Stop") {
                IDictionary<string, object> response = new Dictionary<string, object>();
                bool repeated = StopHookActive(input, serializer);
                bool skipped = !repeated && settingsStore.ConsumeSkipNextCompletion();
                if (!repeated && settings.CompletionSuggestionsEnabled && !skipped) {
                    response["decision"] = "block";
                    response["reason"] = CompletionContinuationReason(settings.CompletionOptionCount);
                }
                output.WriteLine(serializer.Serialize(response));
                return;
            }

            IDictionary<string, object> hookSpecificOutput = new Dictionary<string, object>();
            hookSpecificOutput["hookEventName"] = canonicalEvent;
            hookSpecificOutput["additionalContext"] = RoutingPolicy + " " + CompletionPolicy(settings);

            IDictionary<string, object> contextResponse = new Dictionary<string, object>();
            contextResponse["hookSpecificOutput"] = hookSpecificOutput;
            output.WriteLine(serializer.Serialize(contextResponse));
        }

        private static string CompletionPolicy(CueSettings settings) {
            if (!settings.CompletionSuggestionsEnabled) {
                return "Completed-task follow-up prompts are disabled in Codex Cue settings. Do not invoke next-step-options unless the user explicitly requests suggestions.";
            }
            return "Mandatory completed-task follow-up policy: Whenever the current user-requested task is genuinely complete, use the next-step-options skill before ending the turn. Generate exactly " +
                settings.CompletionOptionCount + " relevant next-task choice" + (settings.CompletionOptionCount == 1 ? "" : "s") +
                " and call ask_options from codex_cue so the user can click one. Treat cancellation or timeout as Skip: finish without asking again. Do not run this workflow for incomplete work, blockers, greetings, status-only replies, or when the user explicitly opted out.";
        }

        private static string CompletionContinuationReason(int optionCount) {
            return "Before ending this turn, check whether the user's requested task is genuinely complete. If required work remains, continue it. If complete and follow-up choices have not already been handled, use $next-step-options now: create exactly " +
                optionCount + " relevant next-task choice" + (optionCount == 1 ? "" : "s") +
                " and call codex_cue.ask_options. Set allowOther: true. Cancellation or timeout means Skip; finish immediately without another follow-up prompt. If the tool was already handled, the task is blocked or incomplete, this is only a greeting/status reply, or the user opted out, finish without prompting.";
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
