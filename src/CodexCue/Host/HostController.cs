using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodexCue.Core;
using CodexCue.Mcp;

namespace CodexCue.Host {
    public sealed class PromptRequestedEventArgs : EventArgs {
        public PromptRequestedEventArgs(PendingPrompt pending) { Pending = pending; }
        public PendingPrompt Pending { get; private set; }
    }

    public sealed class PromptResolvedEventArgs : EventArgs {
        public PromptResolvedEventArgs(string sessionId, string status) {
            SessionId = sessionId;
            Status = status;
        }
        public string SessionId { get; private set; }
        public string Status { get; private set; }
    }

    public sealed class HostController : IDisposable {
        private readonly object sync = new object();
        private readonly RequestQueue queue = new RequestQueue();
        private PendingPrompt active;
        private bool stopped;

        public event EventHandler<PromptRequestedEventArgs> PromptRequested;
        public event EventHandler<PromptResolvedEventArgs> PromptResolved;

        public PendingPrompt Active { get { lock (sync) return active; } }

        public Task<OptionResult> EnqueueAsync(OptionRequest request, CancellationToken cancellationToken) {
            PendingPrompt pending = new PendingPrompt(request);
            PendingPrompt toActivate;
            lock (sync) {
                if (stopped) throw new PromptClientException("HOST_UNAVAILABLE", "The prompt host is stopping.");
                if ((active != null && String.Equals(active.SessionId, pending.SessionId, StringComparison.Ordinal)) || queue.Contains(pending.SessionId)) {
                    throw new ArgumentException("Duplicate session ID.", "request");
                }
                queue.Enqueue(pending);
                pending.TimeoutTimer = new Timer(delegate { Timeout(pending.SessionId); }, null, request.MaxWaitMs, System.Threading.Timeout.Infinite);
                if (request.AutoResolutionMs.HasValue) {
                    pending.AutoResolutionTimer = new Timer(delegate { AutoResolve(pending.SessionId); }, null,
                        request.AutoResolutionMs.Value, System.Threading.Timeout.Infinite);
                }
                pending.CancellationRegistration = cancellationToken.Register(delegate { Cancel(pending.SessionId); });
                toActivate = PromoteUnsafe();
            }
            RaisePromptRequested(toActivate);
            if (cancellationToken.IsCancellationRequested) Cancel(pending.SessionId);
            return pending.Completion.Task;
        }

        public bool Submit(string sessionId, OptionResult result) {
            if (result == null) throw new ArgumentNullException("result");
            PendingPrompt completed;
            PendingPrompt next;
            lock (sync) {
                if (active == null || !String.Equals(active.SessionId, sessionId, StringComparison.Ordinal)) return false;
                completed = active;
                active = null;
                next = PromoteUnsafe();
            }
            completed.Completion.TrySetResult(result);
            completed.Dispose();
            RaisePromptResolved(completed, result);
            RaisePromptRequested(next);
            return true;
        }

        public bool Cancel(string sessionId) {
            PendingPrompt completed;
            PendingPrompt next = null;
            lock (sync) {
                if (active != null && String.Equals(active.SessionId, sessionId, StringComparison.Ordinal)) {
                    completed = active;
                    active = null;
                    next = PromoteUnsafe();
                } else completed = queue.Cancel(sessionId);
            }
            if (completed == null) return false;
            OptionResult result = ResultFactory.Cancelled(completed.Request);
            completed.Completion.TrySetResult(result);
            completed.Dispose();
            RaisePromptResolved(completed, result);
            RaisePromptRequested(next);
            return true;
        }

        public PromptHostStatus GetStatus() {
            lock (sync) {
                return new PromptHostStatus {
                    ApplicationVersion = "2.2.0",
                    ProtocolVersion = 1,
                    IsRunning = !stopped,
                    ActiveCount = active == null ? 0 : 1,
                    QueuedCount = queue.Count
                };
            }
        }

        public void Stop() {
            List<PendingPrompt> pending = new List<PendingPrompt>();
            lock (sync) {
                if (stopped) return;
                stopped = true;
                if (active != null) { pending.Add(active); active = null; }
                pending.AddRange(queue.Drain());
            }
            foreach (PendingPrompt item in pending) {
                item.Completion.TrySetException(new PromptClientException("HOST_UNAVAILABLE", "The prompt host stopped."));
                item.Dispose();
            }
        }

        public void Dispose() { Stop(); }

        private void Timeout(string sessionId) {
            PendingPrompt completed;
            PendingPrompt next = null;
            lock (sync) {
                if (active != null && String.Equals(active.SessionId, sessionId, StringComparison.Ordinal)) {
                    completed = active;
                    active = null;
                    next = PromoteUnsafe();
                } else completed = queue.Cancel(sessionId);
            }
            if (completed == null) return;
            OptionResult result = ResultFactory.TimedOut(completed.Request);
            completed.Completion.TrySetResult(result);
            completed.Dispose();
            RaisePromptResolved(completed, result);
            RaisePromptRequested(next);
        }

        private void AutoResolve(string sessionId) {
            PendingPrompt completed;
            PendingPrompt next = null;
            lock (sync) {
                if (active != null && String.Equals(active.SessionId, sessionId, StringComparison.Ordinal)) {
                    completed = active;
                    active = null;
                    next = PromoteUnsafe();
                } else completed = queue.Cancel(sessionId);
            }
            if (completed == null) return;
            OptionResult result = ResultFactory.AutoSubmitted(completed.Request);
            completed.Completion.TrySetResult(result);
            completed.Dispose();
            RaisePromptResolved(completed, result);
            RaisePromptRequested(next);
        }

        private PendingPrompt PromoteUnsafe() {
            if (active != null || stopped) return null;
            active = queue.Dequeue();
            return active;
        }

        private void RaisePromptRequested(PendingPrompt pending) {
            if (pending == null || pending.Request.SuppressUi) return;
            EventHandler<PromptRequestedEventArgs> handler = PromptRequested;
            if (handler != null) handler(this, new PromptRequestedEventArgs(pending));
        }

        private void RaisePromptResolved(PendingPrompt pending, OptionResult result) {
            EventHandler<PromptResolvedEventArgs> handler = PromptResolved;
            if (handler != null) handler(this, new PromptResolvedEventArgs(pending.SessionId, result.Status));
        }
    }
}
