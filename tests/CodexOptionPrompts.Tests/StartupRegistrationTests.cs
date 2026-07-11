using CodexOptionPrompts.Install;

namespace CodexOptionPrompts.Tests {
    internal static class StartupRegistrationTests {
        public static void Register(TestRegistry tests) {
            tests.Add("StartupRegistration builds a quoted native host command", delegate {
                string command = StartupRegistration.Command(@"C:\Program Files\CodexOptionPrompts\CodexOptionPrompts.exe");
                Assert.Equal("\"C:\\Program Files\\CodexOptionPrompts\\CodexOptionPrompts.exe\" --host", command);
            });
        }
    }
}
