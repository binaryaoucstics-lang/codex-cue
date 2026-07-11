using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Host;
using CodexOptionPrompts.Mcp;

namespace CodexOptionPrompts.Tests {
    internal static class RequestQueueTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Queue is FIFO and cancellation removes one request", delegate {
                RequestQueue queue = new RequestQueue();
                queue.Enqueue(HostFixtures.Pending("one"));
                queue.Enqueue(HostFixtures.Pending("two"));
                queue.Cancel("one");
                Assert.Equal("two", queue.Peek().SessionId);
            });

            tests.Add("Queue rejects duplicate session IDs", delegate {
                RequestQueue queue = new RequestQueue();
                queue.Enqueue(HostFixtures.Pending("same"));
                Assert.Throws<System.ArgumentException>(delegate { queue.Enqueue(HostFixtures.Pending("same")); });
            });

            tests.Add("Queue host controller activates requests one at a time", delegate {
                HostController controller = new HostController();
                List<string> activated = new List<string>();
                controller.PromptRequested += delegate(object sender, PromptRequestedEventArgs args) {
                    activated.Add(args.Pending.SessionId);
                };

                Task<OptionResult> first = controller.EnqueueAsync(HostFixtures.Request("one"), CancellationToken.None);
                Task<OptionResult> second = controller.EnqueueAsync(HostFixtures.Request("two"), CancellationToken.None);
                Assert.Equal(1, activated.Count);
                Assert.Equal("one", activated[0]);
                controller.Submit("one", HostFixtures.Submitted("one"));
                Assert.Equal(2, activated.Count);
                Assert.Equal("two", activated[1]);
                controller.Submit("two", HostFixtures.Submitted("two"));
                Assert.Equal("submitted", first.GetAwaiter().GetResult().Status);
                Assert.Equal("submitted", second.GetAwaiter().GetResult().Status);
                controller.Dispose();
            });

            tests.Add("Queue cancellation completes only matching request", delegate {
                HostController controller = new HostController();
                Task<OptionResult> first = controller.EnqueueAsync(HostFixtures.Request("one"), CancellationToken.None);
                Task<OptionResult> second = controller.EnqueueAsync(HostFixtures.Request("two"), CancellationToken.None);
                controller.Cancel("one");
                Assert.Equal("cancelled", first.GetAwaiter().GetResult().Status);
                Assert.False(second.IsCompleted);
                controller.Submit("two", HostFixtures.Submitted("two"));
                Assert.Equal("submitted", second.GetAwaiter().GetResult().Status);
                controller.Dispose();
            });

            tests.Add("Queue timeout completes without partial answers", delegate {
                HostController controller = new HostController();
                OptionRequest request = HostFixtures.Request("timeout");
                request.MaxWaitMs = 25;
                OptionResult result = controller.EnqueueAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                Assert.Equal("timed_out", result.Status);
                Assert.Equal(0, result.Answers.Count);
                controller.Dispose();
            });

            tests.Add("Queue auto resolution submits recommendations without showing suppressed UI", delegate {
                HostController controller = new HostController();
                int shown = 0;
                controller.PromptRequested += delegate { shown++; };
                OptionRequest request = HostFixtures.Request("auto-none");
                request.Questions[0].Options[0].Recommended = true;
                request.SuppressUi = true;
                request.AutoResolutionMs = 25;
                request.MaxWaitMs = 1000;

                OptionResult result = controller.EnqueueAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                Assert.Equal("submitted", result.Status);
                Assert.Equal("auto", result.Resolution);
                Assert.Equal("a", result.Answers[0].SelectedOptionIds[0]);
                Assert.Equal(0, shown);
                controller.Dispose();
            });

            tests.Add("Queue host exit faults every pending request", delegate {
                HostController controller = new HostController();
                Task<OptionResult> active = controller.EnqueueAsync(HostFixtures.Request("active"), CancellationToken.None);
                Task<OptionResult> queued = controller.EnqueueAsync(HostFixtures.Request("queued"), CancellationToken.None);
                controller.Stop();
                Assert.Throws<PromptClientException>(delegate { active.GetAwaiter().GetResult(); });
                Assert.Throws<PromptClientException>(delegate { queued.GetAwaiter().GetResult(); });
                controller.Dispose();
            });
        }
    }
}
