using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CodexCue.Mcp;

namespace CodexCue.Ipc {
    public sealed class HostLauncher {
        private readonly PipeIdentity identity;
        private readonly bool mayLaunch;

        public HostLauncher(PipeIdentity identity) : this(identity, true) { }

        private HostLauncher(PipeIdentity identity, bool mayLaunch) {
            if (identity == null) throw new ArgumentNullException("identity");
            this.identity = identity;
            this.mayLaunch = mayLaunch;
        }

        public static HostLauncher ForExistingPipe(PipeIdentity identity) {
            return new HostLauncher(identity, false);
        }

        public async Task<PipeEnvelope> SendAsync(PipeEnvelope request, CancellationToken cancellationToken) {
            try {
                return await PipeConnection.SendAsync(identity, request, 750, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) { throw; }
            catch (Exception first) {
                if (!mayLaunch) {
                    throw new PromptClientException("HOST_UNAVAILABLE", "The prompt host is unavailable.", first);
                }
            }

            try {
                StartHost();
                return await PipeConnection.SendAsync(identity, request, 5000, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) { throw; }
            catch (Exception error) {
                throw new PromptClientException("HOST_START_FAILED", "The prompt host could not be started.", error);
            }
        }

        internal static string ResolveExecutable() {
            string installed = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "CodexCue", "CodexCue.exe");
            if (File.Exists(installed)) return installed;
            return Assembly.GetExecutingAssembly().Location;
        }

        private static void StartHost() {
            ProcessStartInfo start = new ProcessStartInfo {
                FileName = ResolveExecutable(),
                Arguments = "--host",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(ResolveExecutable())
            };
            Process process = Process.Start(start);
            if (process == null) throw new InvalidOperationException("The host process did not start.");
            process.Dispose();
        }
    }
}
