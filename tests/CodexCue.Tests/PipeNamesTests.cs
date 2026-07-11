using CodexCue.Ipc;

namespace CodexCue.Tests {
    internal static class PipeNamesTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Pipe and mutex are stable per SID and version", delegate {
                PipeIdentity first = PipeNames.ForSid("S-1-5-21-test", 1);
                PipeIdentity second = PipeNames.ForSid("S-1-5-21-test", 1);
                Assert.Equal(first.PipeName, second.PipeName);
                Assert.Equal(first.MutexName, second.MutexName);
                Assert.True(first.PipeName.StartsWith("CodexCue.v1."));
                Assert.True(first.MutexName.StartsWith("Local\\CodexCue.Host.v1."));
                Assert.False(first.PipeName.Contains("S-1-5-21-test"));
            });

            tests.Add("Pipe protocol rejects payloads above one MiB", delegate {
                Assert.Throws<System.IO.InvalidDataException>(delegate {
                    PipeProtocol.ReadAsync(HostFixtures.OversizedFrame(), System.Threading.CancellationToken.None)
                        .GetAwaiter().GetResult();
                });
            });
        }
    }
}
