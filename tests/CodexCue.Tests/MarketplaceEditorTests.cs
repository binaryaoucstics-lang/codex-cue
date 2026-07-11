using System.Collections.Generic;
using CodexCue.Install;
using CodexCue.Mcp;

namespace CodexCue.Tests {
    internal static class MarketplaceEditorTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Marketplace merge preserves unknown fields and plugin order", delegate {
                string input = Fixtures.Text("marketplace-existing.json");
                string output = MarketplaceEditor.Upsert(input, InstallFixtures.Entry());
                IDictionary<string, object> parsed = new JsonCodec().ParseObject(output);
                Assert.Equal("preserved", MarketplaceAssertions.UnknownRootValue(parsed));
                Assert.SequenceEqual(new[] { "first", "codex-cue", "last" }, MarketplaceAssertions.PluginNames(parsed));
                Assert.True(MarketplaceAssertions.IsCanonicalManagedEntry(parsed));
            });

            tests.Add("Marketplace append and remove preserve unrelated entries", delegate {
                string input = "{\"name\":\"personal\",\"unknownRoot\":\"preserved\",\"plugins\":[{\"name\":\"first\"}]}";
                string installed = MarketplaceEditor.Upsert(input, InstallFixtures.Entry());
                string removed = MarketplaceEditor.RemoveManaged(installed, "codex-cue");
                IDictionary<string, object> parsed = new JsonCodec().ParseObject(removed);
                Assert.Equal("preserved", MarketplaceAssertions.UnknownRootValue(parsed));
                Assert.SequenceEqual(new[] { "first" }, MarketplaceAssertions.PluginNames(parsed));
            });
        }
    }
}
