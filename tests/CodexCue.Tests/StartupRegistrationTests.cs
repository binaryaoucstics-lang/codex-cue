using CodexCue.Install;

namespace CodexCue.Tests {
    internal static class StartupRegistrationTests {
        public static void Register(TestRegistry tests) {
            tests.Add("StartupRegistration builds a quoted native host command", delegate {
                string command = StartupRegistration.Command(@"C:\Program Files\CodexCue\CodexCue.exe");
                Assert.Equal("\"C:\\Program Files\\CodexCue\\CodexCue.exe\" --host", command);
            });
        }
    }
}
