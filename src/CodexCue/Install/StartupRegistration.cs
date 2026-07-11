using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace CodexCue.Install {
    public static class StartupRegistration {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CodexCue";
        private const string LegacyValueName = "CodexOptionPrompts";

        public static string Command(string executable) {
            if (String.IsNullOrWhiteSpace(executable)) throw new ArgumentException("An executable path is required.", "executable");
            return "\"" + Path.GetFullPath(executable) + "\" --host";
        }

        public static void Enable(string executable) {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)) {
                if (key == null) throw new InvalidOperationException("The current-user startup registry key is unavailable.");
                key.SetValue(ValueName, Command(executable), RegistryValueKind.String);
                key.DeleteValue(LegacyValueName, false);
            }
        }

        public static void Disable(string executable) {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)) {
                if (key == null) return;
                string current = Convert.ToString(key.GetValue(ValueName, null));
                if (String.Equals(current, Command(executable), StringComparison.OrdinalIgnoreCase)) {
                    key.DeleteValue(ValueName, false);
                }
            }
        }

        public static void StartHost(string executable) {
            ProcessStartInfo start = new ProcessStartInfo {
                FileName = Path.GetFullPath(executable),
                Arguments = "--host",
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable)),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process process = Process.Start(start);
            if (process == null) throw new InvalidOperationException("The desktop prompt host did not start.");
            process.Dispose();
        }
    }
}
