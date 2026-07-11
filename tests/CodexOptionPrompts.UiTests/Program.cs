using System;
using System.IO;
using CodexOptionPrompts.Tests;

namespace CodexOptionPrompts.UiTests {
    internal static class Program {
        [STAThread]
        private static int Main(string[] args) {
            string filter = "";
            string captureDirectory = null;
            for (int index = 0; index < args.Length; index++) {
                if (args[index] == "--capture-dir") {
                    if (index + 1 >= args.Length) throw new ArgumentException("Missing capture directory.");
                    captureDirectory = Path.GetFullPath(args[++index]);
                } else filter = args[index];
            }

            TestRegistry tests = new TestRegistry();
            WizardUiTests.Register(tests);
            int result = tests.Run(filter);
            if (result == 0 && captureDirectory != null) WizardUiCaptures.Capture(captureDirectory);
            return result;
        }
    }
}
