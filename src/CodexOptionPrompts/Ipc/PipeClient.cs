using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Mcp;

namespace CodexOptionPrompts.Ipc {
    public sealed class PipePromptClient : IPromptClient {
        private readonly PipeIdentity identity;
        private readonly HostLauncher launcher;

        public PipePromptClient(PipeIdentity identity, HostLauncher launcher) {
            if (identity == null) throw new ArgumentNullException("identity");
            if (launcher == null) throw new ArgumentNullException("launcher");
            this.identity = identity;
            this.launcher = launcher;
        }

        public async Task<OptionResult> AskAsync(OptionRequest request, CancellationToken cancellationToken) {
            if (request == null) throw new ArgumentNullException("request");
            PipeEnvelope response = await launcher.SendAsync(PipeProtocol.Envelope(
                identity.ProtocolVersion, "ask", request.SessionId, PipeProtocol.RequestPayload(request)), cancellationToken).ConfigureAwait(false);
            ThrowIfError(response);
            if (!String.Equals(response.Type, "result", StringComparison.Ordinal)) {
                throw new PromptClientException("PROTOCOL_MISMATCH", "The prompt host returned an unexpected response.");
            }
            return PipeProtocol.ResultFromPayload(response.Payload);
        }

        public async Task<PromptHostStatus> GetStatusAsync(CancellationToken cancellationToken) {
            PipeEnvelope response = await launcher.SendAsync(PipeProtocol.Envelope(
                identity.ProtocolVersion, "status", null, new Dictionary<string, object>()), cancellationToken).ConfigureAwait(false);
            ThrowIfError(response);
            if (!String.Equals(response.Type, "status_result", StringComparison.Ordinal)) {
                throw new PromptClientException("PROTOCOL_MISMATCH", "The prompt host returned an unexpected status response.");
            }
            return PipeProtocol.StatusFromPayload(response.Payload);
        }

        private static void ThrowIfError(PipeEnvelope response) {
            if (response == null) throw new PromptClientException("PIPE_DISCONNECTED", "The prompt host disconnected.");
            if (!String.Equals(response.Type, "error", StringComparison.Ordinal)) return;
            object code;
            object message;
            response.Payload.TryGetValue("code", out code);
            response.Payload.TryGetValue("message", out message);
            throw new PromptClientException(Convert.ToString(code), Convert.ToString(message));
        }
    }

    internal static class PipeConnection {
        public static async Task<PipeEnvelope> SendAsync(PipeIdentity identity, PipeEnvelope request, int connectTimeoutMs, CancellationToken cancellationToken) {
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", identity.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous)) {
                await pipe.ConnectAsync(connectTimeoutMs, cancellationToken).ConfigureAwait(false);
                await PipeProtocol.WriteAsync(pipe, request, cancellationToken).ConfigureAwait(false);
                using (cancellationToken.Register(delegate { try { pipe.Dispose(); } catch { } })) {
                    try {
                        return await PipeProtocol.ReadAsync(pipe, cancellationToken).ConfigureAwait(false);
                    } catch (ObjectDisposedException) {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    } catch (IOException) {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                }
            }
        }
    }
}
