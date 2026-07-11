using System;
using System.IO;
using CodexCue.Tests;

namespace CodexCue.UiTests {
    internal static class Program {
        [STAThread]
        private static int Main(string[] args) {
            string filter = "";
            string captureDirectory = null;
            string settingsCapture = null;
            for (int index = 0; index < args.Length; index++) {
                if (args[index] == "--capture-dir") {
                    if (index + 1 >= args.Length) throw new ArgumentException("Missing capture directory.");
                    captureDirectory = Path.GetFullPath(args[++index]);
                } else if (args[index] == "--capture-settings") {
                    if (index + 1 >= args.Length) throw new ArgumentException("Missing settings capture path.");
                    settingsCapture = Path.GetFullPath(args[++index]);
                } else filter = args[index];
            }

            if (settingsCapture != null) {
                WizardUiCaptures.CaptureSettings(settingsCapture);
                return 0;
            }

            TestRegistry tests = new TestRegistry();
            WizardUiTests.Register(tests);
            int result = tests.Run(filter);
            if (result == 0 && captureDirectory != null) WizardUiCaptures.Capture(captureDirectory);
            return result;
        }
    }
}
