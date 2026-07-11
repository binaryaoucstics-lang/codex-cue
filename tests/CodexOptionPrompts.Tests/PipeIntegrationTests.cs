using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;

namespace CodexOptionPrompts.Tests {
    internal static class PipeIntegrationTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Pipe round trip is current user only", delegate {
                using (PipeIntegrationFixture fixture = PipeIntegrationFixture.Start()) {
                    OptionResult result = fixture.Client.AskAsync(HostFixtures.Request("round-trip"), CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Assert.Equal("submitted", result.Status);
                    Assert.True(fixture.ServerSecurityContainsOnlyCurrentUser);
                }
            });

            tests.Add("Pipe status round trip contains counts only", delegate {
                using (PipeIntegrationFixture fixture = PipeIntegrationFixture.Start()) {
                    PromptHostStatus status = fixture.Client.GetStatusAsync(CancellationToken.None).GetAwaiter().GetResult();
                    Assert.True(status.IsRunning);
                    Assert.Equal(0, status.ActiveCount);
                    Assert.Equal(0, status.QueuedCount);
                }
            });

            tests.Add("Pipe two clients complete in FIFO order", delegate {
                using (PipeIntegrationFixture fixture = PipeIntegrationFixture.StartManual()) {
                    Task<OptionResult> first = fixture.Client.AskAsync(HostFixtures.Request("one"), CancellationToken.None);
                    Assert.True(SpinWait.SpinUntil(delegate { return fixture.Controller.Active != null; }, 1500));
                    Task<OptionResult> second = fixture.Client.AskAsync(HostFixtures.Request("two"), CancellationToken.None);
                    Assert.True(SpinWait.SpinUntil(delegate { return fixture.Controller.GetStatus().QueuedCount == 1; }, 1500));
                    Assert.Equal("one", fixture.Controller.Active.SessionId);
                    fixture.Controller.Submit("one", HostFixtures.Submitted("one"));
                    Assert.True(SpinWait.SpinUntil(delegate {
                        return fixture.Controller.Active != null && fixture.Controller.Active.SessionId == "two";
                    }, 1500));
                    fixture.Controller.Submit("two", HostFixtures.Submitted("two"));
                    Assert.Equal("one", first.GetAwaiter().GetResult().SessionId);
                    Assert.Equal("two", second.GetAwaiter().GetResult().SessionId);
                }
            });

            tests.Add("Pipe client cancellation clears active request", delegate {
                using (PipeIntegrationFixture fixture = PipeIntegrationFixture.StartManual()) {
                    CancellationTokenSource cancellation = new CancellationTokenSource();
                    Task<OptionResult> result = fixture.Client.AskAsync(HostFixtures.Request("cancel-me"), cancellation.Token);
                    Assert.True(SpinWait.SpinUntil(delegate { return fixture.Controller.Active != null; }, 1500));
                    cancellation.Cancel();
                    Assert.Throws<System.OperationCanceledException>(delegate { result.GetAwaiter().GetResult(); });
                    Assert.True(SpinWait.SpinUntil(delegate { return fixture.Controller.Active == null; }, 1500));
                    cancellation.Dispose();
                }
            });
        }
    }
}
