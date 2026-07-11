using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Host;

namespace CodexOptionPrompts.Ipc {
    public sealed class PipeServer : IDisposable {
        private readonly PipeIdentity identity;
        private readonly HostController controller;
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();
        private readonly object sync = new object();
        private NamedPipeServerStream listener;
        private Task acceptLoop;

        public PipeServer(PipeIdentity identity, HostController controller) {
            if (identity == null) throw new ArgumentNullException("identity");
            if (controller == null) throw new ArgumentNullException("controller");
            this.identity = identity;
            this.controller = controller;
            Security = CurrentUserSecurity();
        }

        public PipeSecurity Security { get; private set; }

        public void Start() {
            lock (sync) {
                if (acceptLoop != null) throw new InvalidOperationException("The pipe server is already running.");
                acceptLoop = AcceptLoopAsync(stopping.Token);
            }
        }

        public void Dispose() {
            stopping.Cancel();
            lock (sync) {
                if (listener != null) {
                    try { listener.Dispose(); } catch { }
                    listener = null;
                }
            }
            try { if (acceptLoop != null) acceptLoop.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            stopping.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                NamedPipeServerStream pipe = CreatePipe();
                lock (sync) listener = pipe;
                try {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    pipe.Dispose();
                    break;
                } catch (ObjectDisposedException) {
                    break;
                }
                lock (sync) if (Object.ReferenceEquals(listener, pipe)) listener = null;
                HandleConnectionAndDisposeAsync(pipe, cancellationToken);
            }
        }

        private async void HandleConnectionAndDisposeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken) {
            using (pipe) {
                Exception failure = null;
                try { await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (IOException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception error) { failure = error; }
                if (failure != null) {
                    try { await WriteErrorAsync(pipe, null, "HOST_UNAVAILABLE", failure.Message, CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception) { }
                }
            }
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken serverCancellation) {
            PipeEnvelope envelope = await PipeProtocol.ReadAsync(pipe, serverCancellation).ConfigureAwait(false);
            if (envelope.ProtocolVersion != identity.ProtocolVersion) {
                await WriteErrorAsync(pipe, envelope.SessionId, "PROTOCOL_MISMATCH", "IPC protocol version mismatch.", serverCancellation).ConfigureAwait(false);
                return;
            }

            if (String.Equals(envelope.Type, "status", StringComparison.Ordinal)) {
                await PipeProtocol.WriteAsync(pipe, PipeProtocol.Envelope(identity.ProtocolVersion, "status_result", null,
                    PipeProtocol.StatusPayload(controller.GetStatus())), serverCancellation).ConfigureAwait(false);
                return;
            }
            if (!String.Equals(envelope.Type, "ask", StringComparison.Ordinal)) {
                await WriteErrorAsync(pipe, envelope.SessionId, "PROTOCOL_MISMATCH", "Unknown IPC message type.", serverCancellation).ConfigureAwait(false);
                return;
            }

            OptionRequest request = null;
            string invalidMessage = null;
            try {
                request = PipeProtocol.RequestFromPayload(envelope.Payload);
                ValidationResult validation = RequestValidator.Validate(request);
                if (!validation.IsValid) {
                    await WriteErrorAsync(pipe, envelope.SessionId, validation.Code, validation.Message, serverCancellation).ConfigureAwait(false);
                    return;
                }
            } catch (Exception error) { invalidMessage = error.Message; }
            if (invalidMessage != null) {
                await WriteErrorAsync(pipe, envelope.SessionId, "INVALID_REQUEST", invalidMessage, serverCancellation).ConfigureAwait(false);
                return;
            }

            using (CancellationTokenSource connection = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation)) {
                Task<OptionResult> resultTask = controller.EnqueueAsync(request, connection.Token);
                byte[] probe = new byte[1];
                Task<int> disconnected = pipe.ReadAsync(probe, 0, 1, connection.Token);
                Task winner = await Task.WhenAny(resultTask, disconnected).ConfigureAwait(false);
                if (Object.ReferenceEquals(winner, disconnected)) {
                    connection.Cancel();
                    controller.Cancel(request.SessionId);
                    return;
                }
                OptionResult result = await resultTask.ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, PipeProtocol.Envelope(identity.ProtocolVersion, "result", request.SessionId,
                    PipeProtocol.ResultPayload(result)), serverCancellation).ConfigureAwait(false);
                connection.Cancel();
            }
        }

        private Task WriteErrorAsync(Stream pipe, string sessionId, string code, string message, CancellationToken cancellationToken) {
            return PipeProtocol.WriteAsync(pipe, PipeProtocol.Envelope(identity.ProtocolVersion, "error", sessionId,
                new Dictionary<string, object> { { "code", code }, { "message", message } }), cancellationToken);
        }

        private NamedPipeServerStream CreatePipe() {
            return new NamedPipeServerStream(
                identity.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                Security);
        }

        private static PipeSecurity CurrentUserSecurity() {
            SecurityIdentifier sid = WindowsIdentity.GetCurrent().User;
            if (sid == null) throw new InvalidOperationException("The current Windows SID is unavailable.");
            PipeSecurity security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(sid);
            security.AddAccessRule(new PipeAccessRule(
                sid,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            return security;
        }
    }
}
