using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using CodexOptionPrompts.Hooks;

namespace CodexOptionPrompts.Tests {
    internal static class HookContextWriterTests {
        public static void Register(TestRegistry tests) {
            tests.Add("HookContextWriter emits SessionStart developer context", delegate {
                AssertHook("session-start", "SessionStart");
            });
            tests.Add("HookContextWriter emits UserPromptSubmit developer context", delegate {
                AssertHook("user-prompt-submit", "UserPromptSubmit");
            });
            tests.Add("HookContextWriter rejects unsupported events", delegate {
                Assert.Throws<ArgumentException>(delegate {
                    HookContextWriter.Write("stop", new StringWriter());
                });
            });
        }

        private static void AssertHook(string inputEvent, string expectedEvent) {
            StringWriter output = new StringWriter();
            HookContextWriter.Write(inputEvent, output);
            IDictionary<string, object> root = new JavaScriptSerializer()
                .DeserializeObject(output.ToString()) as IDictionary<string, object>;
            Assert.True(root != null);
            IDictionary<string, object> specific = root["hookSpecificOutput"] as IDictionary<string, object>;
            Assert.True(specific != null);
            Assert.Equal(expectedEvent, specific["hookEventName"] as string);
            string context = specific["additionalContext"] as string;
            Assert.True(context != null && context.IndexOf("codex_option_prompts", StringComparison.Ordinal) >= 0);
            Assert.True(context.IndexOf("ask_options", StringComparison.Ordinal) >= 0);
            Assert.True(context.IndexOf("open-ended", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.True(context.IndexOf("Never use PowerShell", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
