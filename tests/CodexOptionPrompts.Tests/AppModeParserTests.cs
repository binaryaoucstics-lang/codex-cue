using CodexOptionPrompts.Application;

namespace CodexOptionPrompts.Tests {
    internal static class AppModeParserTests {
        public static void Register(TestRegistry tests) {
            tests.Add("AppModeParser defaults to host", delegate {
                AppModeOptions result = AppModeParser.Parse(new string[0]);
                Assert.Equal(AppMode.Host, result.Mode);
            });
            tests.Add("AppModeParser recognizes every supported mode", delegate {
                Assert.Equal(AppMode.Host, AppModeParser.Parse(new[] { "--host" }).Mode);
                Assert.Equal(AppMode.Mcp, AppModeParser.Parse(new[] { "--mcp" }).Mode);
                Assert.Equal(AppMode.Hook, AppModeParser.Parse(new[] { "--hook", "session-start" }).Mode);
                Assert.Equal(AppMode.InstallPlugin, AppModeParser.Parse(new[] { "--install-plugin" }).Mode);
                Assert.Equal(AppMode.UninstallPlugin, AppModeParser.Parse(new[] { "--uninstall-plugin" }).Mode);
                Assert.Equal(AppMode.Demo, AppModeParser.Parse(new[] { "--demo" }).Mode);
            });
            tests.Add("AppModeParser parses automation and test home flags", delegate {
                AppModeOptions result = AppModeParser.Parse(new[] { "--demo", "--automation", "--test-home", "C:\\temp\\option-prompts" });
                Assert.True(result.Automation);
                Assert.Equal("C:\\temp\\option-prompts", result.TestHome);
            });
            tests.Add("AppModeParser captures hook event", delegate {
                AppModeOptions result = AppModeParser.Parse(new[] { "--hook", "user-prompt-submit" });
                Assert.Equal(AppMode.Hook, result.Mode);
                Assert.Equal("user-prompt-submit", result.HookEvent);
            });
            tests.Add("AppModeParser rejects unknown and incomplete arguments", delegate {
                Assert.Throws<System.ArgumentException>(delegate { AppModeParser.Parse(new[] { "--unknown" }); });
                Assert.Throws<System.ArgumentException>(delegate { AppModeParser.Parse(new[] { "--test-home" }); });
                Assert.Throws<System.ArgumentException>(delegate { AppModeParser.Parse(new[] { "--hook" }); });
            });
        }
    }
}
