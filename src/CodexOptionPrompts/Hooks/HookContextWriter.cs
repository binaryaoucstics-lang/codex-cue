using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CodexOptionPrompts.Hooks {
    public static class HookContextWriter {
        public const string RoutingPolicy =
            "Mandatory user-question routing policy: Any time you would ask the user a question or request a response, clarification, confirmation, approval, missing detail, preference, or selection, call the ask_options MCP tool from the codex_option_prompts server immediately. Do not first write the question or choices in assistant prose. This includes both enumerable choices and genuinely open-ended questions. For an open-ended question, send one question with options: [], allowOther: true, and a useful otherLabel. Put all question and option text directly in the MCP JSON arguments. Never use PowerShell, a shell command, or a temporary file to transport question or option text, and do not read the option-prompts SKILL.md through the shell before calling the tool. Let the task determine the number of questions and options and whether each question is single- or multi-select. Do not call the tool when no user response is needed. If ask_options is unavailable or returns an MCP error, ask one concise plain-text question as the fallback.";

        public static void Write(string hookEvent, TextWriter output) {
            if (output == null) throw new ArgumentNullException("output");
            string canonicalEvent = CanonicalEvent(hookEvent);
            IDictionary<string, object> hookSpecificOutput = new Dictionary<string, object>();
            hookSpecificOutput["hookEventName"] = canonicalEvent;
            hookSpecificOutput["additionalContext"] = RoutingPolicy;

            IDictionary<string, object> response = new Dictionary<string, object>();
            response["hookSpecificOutput"] = hookSpecificOutput;
            output.WriteLine(new JavaScriptSerializer().Serialize(response));
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
            throw new ArgumentException("Unsupported hook event: " + hookEvent, "hookEvent");
        }
    }
}
