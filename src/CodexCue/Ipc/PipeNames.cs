using System;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace CodexCue.Ipc {
    public sealed class PipeIdentity {
        public string PipeName { get; set; }
        public string MutexName { get; set; }
        public int ProtocolVersion { get; set; }
    }

    public static class PipeNames {
        public static PipeIdentity Current(int protocolVersion) {
            SecurityIdentifier sid = WindowsIdentity.GetCurrent().User;
            if (sid == null) throw new InvalidOperationException("The current Windows SID is unavailable.");
            return ForSid(sid.Value, protocolVersion);
        }

        public static PipeIdentity ForSid(string sid, int protocolVersion) {
            if (String.IsNullOrWhiteSpace(sid)) throw new ArgumentException("A SID is required.", "sid");
            if (protocolVersion <= 0) throw new ArgumentOutOfRangeException("protocolVersion");
            string suffix = Hash(sid);
            return new PipeIdentity {
                ProtocolVersion = protocolVersion,
                PipeName = "CodexCue.v" + protocolVersion + "." + suffix,
                MutexName = "Local\\CodexCue.Host.v" + protocolVersion + "." + suffix
            };
        }

        private static string Hash(string value) {
            using (SHA256 algorithm = SHA256.Create()) {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder result = new StringBuilder(24);
                for (int index = 0; index < 12; index++) result.Append(hash[index].ToString("x2"));
                return result.ToString();
            }
        }
    }
}
