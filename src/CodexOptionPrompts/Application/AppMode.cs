namespace CodexOptionPrompts.Application {
    public enum AppMode {
        Host,
        Mcp,
        Hook,
        InstallPlugin,
        UninstallPlugin,
        Demo
    }

    public sealed class AppModeOptions {
        public AppMode Mode { get; set; }
        public bool Automation { get; set; }
        public string TestHome { get; set; }
        public string HookEvent { get; set; }
    }
}
