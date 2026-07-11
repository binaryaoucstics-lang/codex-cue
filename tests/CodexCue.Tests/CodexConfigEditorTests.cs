using System;
using CodexCue.Install;

namespace CodexCue.Tests {
    internal static class CodexConfigEditorTests {
        public static void Register(TestRegistry tests) {
            tests.Add("CodexConfigEditor appends direct MCP approval and preserves config", delegate {
                string source = "model = \"gpt-5\"\r\n\r\n[features]\r\nhooks = true\r\n";
                string result = CodexConfigEditor.EnableDirectQuestions(source);
                Assert.True(result.Contains("model = \"gpt-5\""));
                Assert.True(result.Contains("[plugins.\"codex-cue@personal\".mcp_servers.codex_cue]"));
                Assert.True(result.Contains("default_tools_approval_mode = \"approve\""));
            });
            tests.Add("CodexConfigEditor upgrades existing approval idempotently", delegate {
                string source = "[plugins.\"codex-cue@personal\".mcp_servers.codex_cue]\n" +
                    "enabled = false\ndefault_tools_approval_mode = \"auto\"\n";
                string once = CodexConfigEditor.EnableDirectQuestions(source);
                string twice = CodexConfigEditor.EnableDirectQuestions(once);
                Assert.Equal(once, twice);
                Assert.True(once.Contains("enabled = true"));
                Assert.True(once.Contains("default_tools_approval_mode = \"approve\""));
                Assert.Equal(1, Count(once, "default_tools_approval_mode"));
            });
            tests.Add("CodexConfigEditor removes legacy plugin approval section", delegate {
                string source = "[plugins.\"codex-option-prompts@personal\".mcp_servers.codex_option_prompts]\n" +
                    "enabled = true\ndefault_tools_approval_mode = \"approve\"\n\n[features]\nhooks = true\n";
                string result = CodexConfigEditor.EnableDirectQuestions(source);
                Assert.False(result.Contains("codex-option-prompts"));
                Assert.True(result.Contains("[plugins.\"codex-cue@personal\".mcp_servers.codex_cue]"));
                Assert.True(result.Contains("[features]"));
            });
        }

        private static int Count(string text, string value) {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0) { count++; offset += value.Length; }
            return count;
        }
    }
}
