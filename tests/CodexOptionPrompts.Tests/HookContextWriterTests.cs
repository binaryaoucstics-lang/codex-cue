using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using CodexOptionPrompts.Hooks;

namespace CodexOptionPrompts.Tests {
    internal static class HookContextWriterTests {
        public static void Register(TestRegistry tests) {
            tests.Add("HookContextWriter emits SessionStart developer context", delegate {
                AssertContextHook("session-start", "SessionStart");
            });
            tests.Add("HookContextWriter emits UserPromptSubmit developer context", delegate {
                AssertContextHook("user-prompt-submit", "UserPromptSubmit");
            });
            tests.Add("HookContextWriter continues first Stop for next-step enforcement", delegate {
                StringWriter output = new StringWriter();
                HookContextWriter.Write("stop", new StringReader("{\"stop_hook_active\":false}"), output);
                IDictionary<string, object> root = Parse(output);
                Assert.Equal("block", root["decision"] as string);
                string reason = root["reason"] as string;
                Assert.True(reason != null && reason.IndexOf("$next-step-options", StringComparison.Ordinal) >= 0);
                Assert.True(reason.IndexOf("2 or 3", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.True(reason.IndexOf("ask_options", StringComparison.Ordinal) >= 0);
            });
            tests.Add("HookContextWriter releases repeated Stop", delegate {
                StringWriter output = new StringWriter();
                HookContextWriter.Write("Stop", new StringReader("{\"stop_hook_active\":true}"), output);
                Assert.Equal(0, Parse(output).Count);
            });
            tests.Add("HookContextWriter rejects unsupported events", delegate {
                Assert.Throws<ArgumentException>(delegate {
                    HookContextWriter.Write("post-tool-use", new StringWriter());
                });
            });
        }

        private static void AssertContextHook(string inputEvent, string expectedEvent) {
            StringWriter output = new StringWriter();
            HookContextWriter.Write(inputEvent, output);
            IDictionary<string, object> root = Parse(output);
            IDictionary<string, object> specific = root["hookSpecificOutput"] as IDictionary<string, object>;
            Assert.True(specific != null);
            Assert.Equal(expectedEvent, specific["hookEventName"] as string);
            string context = specific["additionalContext"] as string;
            Assert.True(context != null && context.IndexOf("codex_option_prompts", StringComparison.Ordinal) >= 0);
            Assert.True(context.IndexOf("ask_options", StringComparison.Ordinal) >= 0);
            Assert.True(context.IndexOf("open-ended", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.True(context.IndexOf("Never use PowerShell", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.True(context.IndexOf("next-step-options", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.True(context.IndexOf("2 or 3", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IDictionary<string, object> Parse(StringWriter output) {
            IDictionary<string, object> root = new JavaScriptSerializer()
                .DeserializeObject(output.ToString()) as IDictionary<string, object>;
            Assert.True(root != null);
            return root;
        }
    }
}
