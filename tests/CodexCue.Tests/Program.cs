namespace CodexCue.Tests {
    internal static class Program {
        private static int Main(string[] args) {
            string filter = args.Length > 0 ? args[0] : "";
            TestRegistry tests = new TestRegistry();
            AppModeParserTests.Register(tests);
            HookContextWriterTests.Register(tests);
            CueSettingsStoreTests.Register(tests);
            CodexConfigEditorTests.Register(tests);
            StartupRegistrationTests.Register(tests);
            RequestNormalizerTests.Register(tests);
            RequestValidatorTests.Register(tests);
            WizardStateTests.Register(tests);
            WizardViewModelTests.Register(tests);
            StdioTransportTests.Register(tests);
            McpServerTests.Register(tests);
            PipeNamesTests.Register(tests);
            RequestQueueTests.Register(tests);
            PipeIntegrationTests.Register(tests);
            LocalizationTests.Register(tests);
            MarketplaceEditorTests.Register(tests);
            BackupManagerTests.Register(tests);
            PluginInstallerTests.Register(tests);
            return tests.Run(filter);
        }
    }
}
