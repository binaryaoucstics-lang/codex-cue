using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CodexOptionPrompts.Install {
    public sealed class CodexLocator {
        public string Find(string activeExecutable) {
            foreach (string candidate in Candidates(activeExecutable)) {
                if (Validate(candidate)) return candidate;
            }
            return null;
        }

        public int RefreshMarketplace(string codexExecutable, string marketplaceName) {
            int upgraded = Run(codexExecutable, "plugin marketplace upgrade " + Quote(marketplaceName));
            if (upgraded == 0 || !String.Equals(marketplaceName, "personal", StringComparison.Ordinal)) return upgraded;
            string userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Run(codexExecutable, "plugin marketplace add " + Quote(userRoot));
        }

        public int InstallPlugin(string codexExecutable, string pluginName, string marketplaceName) {
            return Run(codexExecutable, "plugin add " + Quote(pluginName + "@" + marketplaceName));
        }

        private static IEnumerable<string> Candidates(string activeExecutable) {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();
            Add(result, seen, activeExecutable);
            Add(result, seen, Environment.GetEnvironmentVariable("CODEX_CLI_PATH"));

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string bin = Path.Combine(local, "OpenAI", "Codex", "bin");
            try {
                if (Directory.Exists(bin)) {
                    string[] directories = Directory.GetDirectories(bin);
                    Array.Sort(directories, delegate(string left, string right) {
                        int recent = Directory.GetLastWriteTimeUtc(right).CompareTo(Directory.GetLastWriteTimeUtc(left));
                        return recent != 0 ? recent : StringComparer.OrdinalIgnoreCase.Compare(left, right);
                    });
                    foreach (string directory in directories) Add(result, seen, Path.Combine(directory, "codex.exe"));
                }
            } catch (UnauthorizedAccessException) { }
            Add(result, seen, Path.Combine(bin, "codex.exe"));

            Add(result, seen, Path.Combine(local, "Microsoft", "WindowsApps", "codex.exe"));
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string windowsApps = Path.Combine(programFiles, "WindowsApps");
            try {
                if (Directory.Exists(windowsApps)) {
                    foreach (string package in Directory.GetDirectories(windowsApps, "OpenAI.Codex_*")) {
                        Add(result, seen, Path.Combine(package, "app", "resources", "codex.exe"));
                    }
                }
            } catch (UnauthorizedAccessException) { }

            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in path.Split(Path.PathSeparator)) {
                if (!String.IsNullOrWhiteSpace(part)) Add(result, seen, Path.Combine(part.Trim(), "codex.exe"));
            }
            return result;
        }

        private static void Add(ICollection<string> result, ISet<string> seen, string candidate) {
            if (String.IsNullOrWhiteSpace(candidate)) return;
            string full;
            try { full = Path.GetFullPath(candidate); }
            catch { return; }
            if (seen.Add(full)) result.Add(full);
        }

        private static bool Validate(string candidate) {
            if (!File.Exists(candidate)) return false;
            try {
                ProcessStartInfo start = BaseStart(candidate);
                start.Arguments = "--version";
                using (Process process = Process.Start(start)) {
                    if (process == null) return false;
                    if (!process.WaitForExit(3000)) {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    return process.ExitCode == 0;
                }
            } catch { return false; }
        }

        private static ProcessStartInfo BaseStart(string executable) {
            return new ProcessStartInfo {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private static int Run(string executable, string arguments) {
            if (String.IsNullOrWhiteSpace(executable)) return -1;
            try {
                ProcessStartInfo start = BaseStart(executable);
                start.Arguments = arguments;
                start.RedirectStandardOutput = false;
                start.RedirectStandardError = false;
                using (Process process = Process.Start(start)) {
                    if (process == null) return -1;
                    if (!process.WaitForExit(15000)) {
                        try { process.Kill(); } catch { }
                        return -1;
                    }
                    return process.ExitCode;
                }
            } catch { return -1; }
        }

        private static string Quote(string value) {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
