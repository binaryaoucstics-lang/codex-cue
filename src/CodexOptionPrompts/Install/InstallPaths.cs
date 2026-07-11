using System;
using System.IO;

namespace CodexOptionPrompts.Install {
    public sealed class InstallPaths {
        public string ProgramDirectory { get; set; }
        public string PluginDirectory { get; set; }
        public string MarketplacePath { get; set; }
        public string CodexConfigPath { get; set; }
        public string DataDirectory { get; set; }

        public static InstallPaths Current() {
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new InstallPaths {
                ProgramDirectory = Full(Path.Combine(local, "Programs", "CodexOptionPrompts")),
                PluginDirectory = Full(Path.Combine(user, "plugins", "codex-option-prompts")),
                MarketplacePath = Full(Path.Combine(user, ".agents", "plugins", "marketplace.json")),
                CodexConfigPath = Full(Path.Combine(user, ".codex", "config.toml")),
                DataDirectory = Full(Path.Combine(local, "CodexOptionPrompts"))
            };
        }

        public static InstallPaths ForHome(string home) {
            if (String.IsNullOrWhiteSpace(home)) throw new ArgumentException("A test home is required.", "home");
            string root = Full(home);
            return new InstallPaths {
                ProgramDirectory = Full(Path.Combine(root, "LocalAppData", "Programs", "CodexOptionPrompts")),
                PluginDirectory = Full(Path.Combine(root, "plugins", "codex-option-prompts")),
                MarketplacePath = Full(Path.Combine(root, ".agents", "plugins", "marketplace.json")),
                CodexConfigPath = Full(Path.Combine(root, ".codex", "config.toml")),
                DataDirectory = Full(Path.Combine(root, "LocalAppData", "CodexOptionPrompts"))
            };
        }

        private static string Full(string path) { return Path.GetFullPath(path); }
    }

    public sealed class MarketplacePlugin {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string InstallationPolicy { get; set; }
        public string AuthenticationPolicy { get; set; }
        public string Category { get; set; }
    }

    public sealed class InstallRequest {
        public string StagedProgramPath { get; set; }
        public string StagedPluginPath { get; set; }
        public bool RefreshCodex { get; set; }
    }

    public sealed class UninstallRequest {
        public bool RestoreBackup { get; set; }
    }

    public sealed class InstallException : Exception {
        public InstallException(string code, string message) : base(message) { Code = code; }
        public InstallException(string code, string message, Exception inner) : base(message, inner) { Code = code; }
        public string Code { get; private set; }
    }
}
