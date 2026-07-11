using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using CodexCue.Settings;

namespace CodexCue.Hooks {
    public static class HookContextWriter {
        public const string RoutingPolicy =
            "For every user-facing question or response request, call codex_cue.ask_options instead of asking in prose. Send text only in MCP JSON, never PowerShell, shell, clipboard, or files. Open text uses options:[], allowOther:true. submitted answers; skipped is explicit skip; cancelled is window cancellation; timed_out is no action.";

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
            hookSpecificOutput["additionalContext"] = canonicalEvent == "SessionStart"
                ? RoutingPolicy + " " + CompletionPolicy(settings)
                : RoutingPolicy;

            IDictionary<string, object> contextResponse = new Dictionary<string, object>();
            contextResponse["hookSpecificOutput"] = hookSpecificOutput;
            output.WriteLine(serializer.Serialize(contextResponse));
        }

        private static string CompletionPolicy(CueSettings settings) {
            if (!settings.CompletionSuggestionsEnabled) {
                return "Completion prompts are disabled; do not invoke next-step-options unless requested.";
            }
            return "After genuinely completing work, use $next-step-options once with exactly " +
                settings.CompletionOptionCount + " relevant next-task choice" + (settings.CompletionOptionCount == 1 ? "" : "s") +
                ". Use cancelResult:skipped. Do not prompt for incomplete work, blockers, greetings, status-only replies, or opt-out.";
        }

        private static string CompletionContinuationReason(int optionCount) {
            return "If work is complete and follow-up was not handled, use $next-step-options once with exactly " +
                optionCount + " relevant next-task choice" + (optionCount == 1 ? "" : "s") +
                ", allowOther:true, localized cancelLabel:Skip, cancelResult:skipped. submitted continues; skipped/cancelled/timed_out ends without repeating. Otherwise finish normally.";
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
