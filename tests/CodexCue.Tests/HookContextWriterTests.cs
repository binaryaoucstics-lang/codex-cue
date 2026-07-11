using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using CodexCue.Hooks;
using CodexCue.Settings;

namespace CodexCue.Tests {
    internal static class HookContextWriterTests {
        public static void Register(TestRegistry tests) {
            tests.Add("HookContextWriter emits SessionStart developer context", delegate {
                AssertContextHook("session-start", "SessionStart");
            });
            tests.Add("HookContextWriter emits UserPromptSubmit developer context", delegate {
                AssertContextHook("user-prompt-submit", "UserPromptSubmit");
            });
            tests.Add("HookContextWriter continues first Stop for next-step enforcement", delegate {
                CueSettingsStoreTests.WithStore(delegate(CueSettingsStore store) {
                    store.Save(new CueSettings { CompletionSuggestionsEnabled = true, CompletionOptionCount = 4 });
                    StringWriter output = new StringWriter();
                    HookContextWriter.Write("stop", new StringReader("{\"stop_hook_active\":false}"), output, store);
                    IDictionary<string, object> root = Parse(output);
                    Assert.Equal("block", root["decision"] as string);
                    string reason = root["reason"] as string;
                    Assert.True(reason != null && reason.IndexOf("$next-step-options", StringComparison.Ordinal) >= 0);
                    Assert.True(reason.IndexOf("exactly 4", StringComparison.OrdinalIgnoreCase) >= 0);
                    Assert.True(reason.IndexOf("Skip", StringComparison.Ordinal) >= 0);
                });
            });
            tests.Add("HookContextWriter releases repeated Stop", delegate {
                CueSettingsStoreTests.WithStore(delegate(CueSettingsStore store) {
                    StringWriter output = new StringWriter();
                    HookContextWriter.Write("Stop", new StringReader("{\"stop_hook_active\":true}"), output, store);
                    Assert.Equal(0, Parse(output).Count);
                });
            });
            tests.Add("HookContextWriter consumes skip without blocking", delegate {
                CueSettingsStoreTests.WithStore(delegate(CueSettingsStore store) {
                    store.Save(new CueSettings { CompletionSuggestionsEnabled = true, CompletionOptionCount = 3, SkipNextCompletion = true });
                    StringWriter output = new StringWriter();
                    HookContextWriter.Write("Stop", new StringReader("{\"stop_hook_active\":false}"), output, store);
                    Assert.Equal(0, Parse(output).Count);
                    Assert.False(store.Load().SkipNextCompletion);
                });
            });
            tests.Add("HookContextWriter honors disabled completion prompts", delegate {
                CueSettingsStoreTests.WithStore(delegate(CueSettingsStore store) {
                    store.Save(new CueSettings { CompletionSuggestionsEnabled = false, CompletionOptionCount = 3 });
                    StringWriter output = new StringWriter();
                    HookContextWriter.Write("Stop", new StringReader("{}"), output, store);
                    Assert.Equal(0, Parse(output).Count);
                });
            });
            tests.Add("HookContextWriter rejects unsupported events", delegate {
                Assert.Throws<ArgumentException>(delegate {
                    HookContextWriter.Write("post-tool-use", new StringWriter());
                });
            });
        }

        private static void AssertContextHook(string inputEvent, string expectedEvent) {
            CueSettingsStoreTests.WithStore(delegate(CueSettingsStore store) {
                store.Save(new CueSettings { CompletionSuggestionsEnabled = true, CompletionOptionCount = 5 });
                StringWriter output = new StringWriter();
                HookContextWriter.Write(inputEvent, new StringReader("{}"), output, store);
                IDictionary<string, object> root = Parse(output);
                IDictionary<string, object> specific = root["hookSpecificOutput"] as IDictionary<string, object>;
                Assert.True(specific != null);
                Assert.Equal(expectedEvent, specific["hookEventName"] as string);
                string context = specific["additionalContext"] as string;
                Assert.True(context != null && context.IndexOf("codex_cue", StringComparison.Ordinal) >= 0);
                Assert.True(context.IndexOf("ask_options", StringComparison.Ordinal) >= 0);
                Assert.True(context.IndexOf("next-step-options", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.True(context.IndexOf("exactly 5", StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        private static IDictionary<string, object> Parse(StringWriter output) {
            IDictionary<string, object> root = new JavaScriptSerializer()
                .DeserializeObject(output.ToString()) as IDictionary<string, object>;
            Assert.True(root != null);
            return root;
        }
    }
}
