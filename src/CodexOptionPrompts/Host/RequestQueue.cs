using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;

namespace CodexOptionPrompts.Host {
    public sealed class PendingPrompt : IDisposable {
        public PendingPrompt(OptionRequest request) {
            if (request == null) throw new ArgumentNullException("request");
            if (String.IsNullOrWhiteSpace(request.SessionId)) throw new ArgumentException("A session ID is required.", "request");
            Request = request;
            Completion = new TaskCompletionSource<OptionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string SessionId { get { return Request.SessionId; } }
        public OptionRequest Request { get; private set; }
        public TaskCompletionSource<OptionResult> Completion { get; private set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
        internal Timer TimeoutTimer { get; set; }
        internal Timer AutoResolutionTimer { get; set; }

        public void Dispose() {
            CancellationRegistration.Dispose();
            if (TimeoutTimer != null) TimeoutTimer.Dispose();
            if (AutoResolutionTimer != null) AutoResolutionTimer.Dispose();
        }
    }

    public sealed class RequestQueue {
        private readonly LinkedList<PendingPrompt> items = new LinkedList<PendingPrompt>();
        private readonly object sync = new object();

        public int Count { get { lock (sync) return items.Count; } }

        public void Enqueue(PendingPrompt pending) {
            if (pending == null) throw new ArgumentNullException("pending");
            lock (sync) {
                if (ContainsUnsafe(pending.SessionId)) throw new ArgumentException("Duplicate session ID.", "pending");
                items.AddLast(pending);
            }
        }

        public PendingPrompt Peek() {
            lock (sync) return items.First == null ? null : items.First.Value;
        }

        public PendingPrompt Dequeue() {
            lock (sync) {
                if (items.First == null) return null;
                PendingPrompt result = items.First.Value;
                items.RemoveFirst();
                return result;
            }
        }

        public PendingPrompt Cancel(string sessionId) {
            lock (sync) {
                LinkedListNode<PendingPrompt> node = items.First;
                while (node != null) {
                    if (String.Equals(node.Value.SessionId, sessionId, StringComparison.Ordinal)) {
                        PendingPrompt result = node.Value;
                        items.Remove(node);
                        return result;
                    }
                    node = node.Next;
                }
                return null;
            }
        }

        public bool Contains(string sessionId) { lock (sync) return ContainsUnsafe(sessionId); }

        public IList<PendingPrompt> Drain() {
            lock (sync) {
                List<PendingPrompt> result = new List<PendingPrompt>(items);
                items.Clear();
                return result;
            }
        }

        private bool ContainsUnsafe(string sessionId) {
            foreach (PendingPrompt item in items) {
                if (String.Equals(item.SessionId, sessionId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
