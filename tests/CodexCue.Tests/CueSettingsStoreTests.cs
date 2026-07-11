using System;
using System.IO;
using CodexCue.Settings;

namespace CodexCue.Tests {
    internal static class CueSettingsStoreTests {
        public static void Register(TestRegistry tests) {
            tests.Add("CueSettingsStore returns defaults", delegate {
                WithStore(delegate(CueSettingsStore store) {
                    CueSettings value = store.Load();
                    Assert.True(value.CompletionSuggestionsEnabled);
                    Assert.Equal(3, value.CompletionOptionCount);
                    Assert.False(value.SkipNextCompletion);
                });
            });
            tests.Add("CueSettingsStore saves and normalizes option count", delegate {
                WithStore(delegate(CueSettingsStore store) {
                    store.Save(new CueSettings { CompletionSuggestionsEnabled = true, CompletionOptionCount = 20 });
                    Assert.Equal(6, store.Load().CompletionOptionCount);
                });
            });
            tests.Add("CueSettingsStore consumes one-time skip", delegate {
                WithStore(delegate(CueSettingsStore store) {
                    store.Save(new CueSettings { CompletionSuggestionsEnabled = true, CompletionOptionCount = 4, SkipNextCompletion = true });
                    Assert.True(store.ConsumeSkipNextCompletion());
                    Assert.False(store.ConsumeSkipNextCompletion());
                    Assert.False(store.Load().SkipNextCompletion);
                });
            });
        }

        public static void WithStore(Action<CueSettingsStore> action) {
            string root = Path.Combine(Path.GetTempPath(), "CodexCueSettingsTests", Guid.NewGuid().ToString("N"));
            try { action(new CueSettingsStore(Path.Combine(root, "settings.json"))); }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
