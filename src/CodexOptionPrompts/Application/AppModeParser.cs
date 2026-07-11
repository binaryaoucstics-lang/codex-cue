using System;

namespace CodexOptionPrompts.Application {
    public static class AppModeParser {
        public static AppModeOptions Parse(string[] args) {
            if (args == null) throw new ArgumentNullException("args");

            AppModeOptions options = new AppModeOptions { Mode = AppMode.Host };
            for (int index = 0; index < args.Length; index++) {
                string value = args[index];
                if (value == "--host") options.Mode = AppMode.Host;
                else if (value == "--mcp") options.Mode = AppMode.Mcp;
                else if (value == "--hook") {
                    if (index + 1 >= args.Length) throw new ArgumentException("Missing value for --hook.", "args");
                    options.Mode = AppMode.Hook;
                    options.HookEvent = args[++index];
                }
                else if (value == "--install-plugin") options.Mode = AppMode.InstallPlugin;
                else if (value == "--uninstall-plugin") options.Mode = AppMode.UninstallPlugin;
                else if (value == "--demo") options.Mode = AppMode.Demo;
                else if (value == "--automation") options.Automation = true;
                else if (value == "--test-home") {
                    if (index + 1 >= args.Length) throw new ArgumentException("Missing value for --test-home.", "args");
                    options.TestHome = args[++index];
                } else throw new ArgumentException("Unknown argument: " + value, "args");
            }
            return options;
        }
    }
}
