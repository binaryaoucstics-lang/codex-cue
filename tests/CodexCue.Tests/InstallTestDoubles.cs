using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodexCue.Install;
using CodexCue.Mcp;

namespace CodexCue.Tests {
    internal sealed class TemporaryHome : IDisposable {
        private const string PrototypeText = "old-node-prototype";
        private const string ModifiedText = "user-modified-skill";

        private TemporaryHome(string path) {
            Path = path;
            Paths = InstallPaths.ForHome(path);
            StagingDirectory = System.IO.Path.Combine(path, "staging");
            StagedProgramPath = System.IO.Path.Combine(StagingDirectory, "CodexCue.exe");
            StagedPluginPath = System.IO.Path.Combine(StagingDirectory, "plugin");
        }

        public string Path { get; private set; }
        public InstallPaths Paths { get; private set; }
        public string StagingDirectory { get; private set; }
        public string StagedProgramPath { get; private set; }
        public string StagedPluginPath { get; private set; }

        public static TemporaryHome WithPrototype() {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodexCue.Tests", Guid.NewGuid().ToString("N"));
            TemporaryHome home = new TemporaryHome(System.IO.Path.GetFullPath(root));
            Directory.CreateDirectory(home.Paths.PluginDirectory);
            Write(home.Paths.PluginDirectory, "scripts\\server.mjs", PrototypeText);
            Write(home.Paths.PluginDirectory, ".codex-plugin\\plugin.json", "{\"name\":\"codex-cue\",\"version\":\"0.1.0\"}");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(home.Paths.MarketplacePath));
            File.WriteAllText(home.Paths.MarketplacePath, Fixtures.Text("marketplace-existing.json"), Encoding.UTF8);
            Write(home.Path, ".codex\\config.toml", "model = \"gpt-5\"\n");

            Directory.CreateDirectory(home.StagingDirectory);
            File.WriteAllBytes(home.StagedProgramPath, new byte[] { 77, 90, 1, 0 });
            Write(home.StagedPluginPath, ".codex-plugin\\plugin.json", "{\"name\":\"codex-cue\",\"version\":\"1.0.0\"}");
            Write(home.StagedPluginPath, "skills\\cue-prompts\\SKILL.md", "final managed skill");
            Write(home.StagedPluginPath, ".mcp.json", "{\"mcpServers\":{}}");
            return home;
        }

        public static TemporaryHome WithInstalledProduct() {
            TemporaryHome home = WithPrototype();
            PluginInstaller.ForTest(home.Path).Install(home.CreateInstallRequest());
            return home;
        }

        public InstallRequest CreateInstallRequest() {
            return new InstallRequest {
                StagedProgramPath = StagedProgramPath,
                StagedPluginPath = StagedPluginPath,
                RefreshCodex = false
            };
        }

        public void ConvertPrototypeToLegacy() {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Paths.LegacyPluginDirectory));
            Directory.Move(Paths.PluginDirectory, Paths.LegacyPluginDirectory);
            string marketplace = File.ReadAllText(Paths.MarketplacePath)
                .Replace("codex-cue-old", "codex-option-prompts-old")
                .Replace("codex-cue", "codex-option-prompts");
            File.WriteAllText(Paths.MarketplacePath, marketplace, new UTF8Encoding(false));
            Write(Path, ".codex\\config.toml",
                "[plugins.\"codex-option-prompts@personal\".mcp_servers.codex_option_prompts]\n" +
                "enabled = true\ndefault_tools_approval_mode = \"approve\"\n");
        }

        public bool PrototypeFilesAreIntact() {
            string server = System.IO.Path.Combine(Paths.PluginDirectory, "scripts", "server.mjs");
            return File.Exists(server) && File.ReadAllText(server) == PrototypeText;
        }

        public bool BackupContainsPrototype() {
            string backups = System.IO.Path.Combine(Paths.DataDirectory, "backups");
            if (!Directory.Exists(backups)) return false;
            foreach (string file in Directory.GetFiles(backups, "server.mjs", SearchOption.AllDirectories)) {
                if (File.ReadAllText(file) == PrototypeText) return true;
            }
            return false;
        }

        public bool PrototypeMarketplaceEntryIsIntact() {
            IDictionary<string, object> root = new JsonCodec().ParseObject(File.ReadAllText(Paths.MarketplacePath, Encoding.UTF8));
            foreach (object value in (object[])root["plugins"]) {
                IDictionary<string, object> plugin = value as IDictionary<string, object>;
                if (plugin == null || Convert.ToString(plugin["name"]) != "codex-cue") continue;
                IDictionary<string, object> source = (IDictionary<string, object>)plugin["source"];
                return Convert.ToString(source["path"]) == "./plugins/codex-cue-old" &&
                    Convert.ToString(plugin["custom"]) == "old-entry";
            }
            return false;
        }

        public bool InstallStatusContainsOnlySafeFields() {
            string path = System.IO.Path.Combine(Paths.DataDirectory, "install-status.json");
            IDictionary<string, object> status = new JsonCodec().ParseObject(File.ReadAllText(path, Encoding.UTF8));
            return status.Count == 6 && status.ContainsKey("productVersion") && status.ContainsKey("codexCli") &&
                status.ContainsKey("marketplaceName") && status.ContainsKey("refreshExitCode") &&
                status.ContainsKey("pluginInstallExitCode") && status.ContainsKey("timestamp");
        }

        public void ModifyInstalledSkill() {
            Write(Paths.PluginDirectory, "skills\\cue-prompts\\SKILL.md", ModifiedText);
        }

        public void CorruptStagedManifest() {
            Write(StagedPluginPath, ".codex-plugin\\plugin.json", "not-json");
        }

        public bool ModifiedSkillStillExists() {
            string path = System.IO.Path.Combine(Paths.PluginDirectory, "skills", "cue-prompts", "SKILL.md");
            return File.Exists(path) && File.ReadAllText(path) == ModifiedText;
        }

        public void Dispose() {
            string full = System.IO.Path.GetFullPath(Path);
            string safeRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodexCue.Tests"));
            if (!full.StartsWith(safeRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Refusing to delete outside the test root.");
            }
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        private static void Write(string root, string relative, string text) {
            string path = System.IO.Path.Combine(root, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    internal static class InstallFixtures {
        public static MarketplacePlugin Entry() {
            return new MarketplacePlugin {
                Name = "codex-cue",
                SourcePath = "./plugins/codex-cue",
                InstallationPolicy = "AVAILABLE",
                AuthenticationPolicy = "ON_INSTALL",
                Category = "Productivity"
            };
        }

        public static void FailAfterBackup(TemporaryHome home) {
            new FailingInstaller(home.Paths).Install(home.CreateInstallRequest());
        }

        private sealed class FailingInstaller : PluginInstaller {
            public FailingInstaller(InstallPaths paths) : base(paths) { }
            protected override void ActivateStagedPlugin(string stagedDirectory, string destinationDirectory) {
                throw new IOException("Injected activation failure.");
            }
        }
    }

    internal static class MarketplaceAssertions {
        public static object UnknownRootValue(IDictionary<string, object> root) { return root["unknownRoot"]; }

        public static IEnumerable<string> PluginNames(IDictionary<string, object> root) {
            List<string> names = new List<string>();
            foreach (object value in (object[])root["plugins"]) {
                names.Add(Convert.ToString(((IDictionary<string, object>)value)["name"]));
            }
            return names;
        }

        public static bool IsCanonicalManagedEntry(IDictionary<string, object> root) {
            foreach (object value in (object[])root["plugins"]) {
                IDictionary<string, object> plugin = value as IDictionary<string, object>;
                if (plugin == null || Convert.ToString(plugin["name"]) != "codex-cue") continue;
                IDictionary<string, object> source = (IDictionary<string, object>)plugin["source"];
                IDictionary<string, object> policy = (IDictionary<string, object>)plugin["policy"];
                return Convert.ToString(source["source"]) == "local" &&
                    Convert.ToString(source["path"]) == "./plugins/codex-cue" &&
                    Convert.ToString(policy["installation"]) == "AVAILABLE" &&
                    Convert.ToString(policy["authentication"]) == "ON_INSTALL" &&
                    Convert.ToString(plugin["category"]) == "Productivity";
            }
            return false;
        }
    }
}
