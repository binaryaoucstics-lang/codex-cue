using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CodexCue.Mcp;

namespace CodexCue.Install {
    public sealed class FileHashRecord {
        public string RelativePath { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class BackupRecord {
        public BackupRecord() { Files = new List<FileHashRecord>(); }
        public string DirectoryPath { get; set; }
        public string OriginalPluginVersion { get; set; }
        public IList<FileHashRecord> Files { get; set; }
        public IDictionary<string, object> OriginalMarketplaceEntry { get; set; }
        public int OriginalMarketplaceIndex { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class BackupManager {
        private readonly InstallPaths paths;
        private readonly JsonCodec codec = new JsonCodec();

        public BackupManager(InstallPaths paths) {
            if (paths == null) throw new ArgumentNullException("paths");
            this.paths = paths;
        }

        public BackupRecord Create() {
            string backups = Path.Combine(paths.DataDirectory, "backups");
            Directory.CreateDirectory(backups);
            string directory = Path.Combine(backups,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directory);

            try {
                BackupRecord record = new BackupRecord {
                    DirectoryPath = directory,
                    CreatedAt = DateTime.UtcNow,
                    OriginalPluginVersion = ReadPluginVersion(paths.PluginDirectory),
                    OriginalMarketplaceIndex = -1
                };
                string pluginBackup = Path.Combine(directory, "plugin");
                if (Directory.Exists(paths.PluginDirectory)) {
                    CopyDirectory(paths.PluginDirectory, pluginBackup);
                    record.Files = HashDirectory(pluginBackup);
                }

                string marketplace = ReadText(paths.MarketplacePath);
                int index;
                record.OriginalMarketplaceEntry = MarketplaceEditor.FindEntry(marketplace, "codex-cue", out index);
                record.OriginalMarketplaceIndex = index;
                WriteManifest(record);
                return record;
            } catch {
                SafeDeleteBackup(directory);
                throw;
            }
        }

        public BackupRecord Latest() {
            string backups = Path.Combine(paths.DataDirectory, "backups");
            if (!Directory.Exists(backups)) return null;
            string[] directories = Directory.GetDirectories(backups);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int index = directories.Length - 1; index >= 0; index--) {
                string manifest = Path.Combine(directories[index], "manifest.json");
                if (File.Exists(manifest)) return Read(directories[index]);
            }
            return null;
        }

        public BackupRecord Read(string directory) {
            string manifestPath = Path.Combine(directory, "manifest.json");
            IDictionary<string, object> manifest = codec.ParseObject(File.ReadAllText(manifestPath, Encoding.UTF8));
            BackupRecord record = new BackupRecord {
                DirectoryPath = directory,
                OriginalPluginVersion = StringValue(manifest, "originalPluginVersion"),
                OriginalMarketplaceIndex = IntValue(manifest, "originalMarketplaceIndex", -1),
                OriginalMarketplaceEntry = ObjectValue(manifest, "originalMarketplaceEntry"),
                CreatedAt = DateValue(manifest, "timestamp")
            };
            object filesValue;
            object[] files = manifest.TryGetValue("files", out filesValue) ? filesValue as object[] : null;
            if (files != null) {
                foreach (object value in files) {
                    IDictionary<string, object> item = value as IDictionary<string, object>;
                    if (item == null) continue;
                    record.Files.Add(new FileHashRecord {
                        RelativePath = StringValue(item, "relativePath"),
                        Sha256 = StringValue(item, "sha256")
                    });
                }
            }
            return record;
        }

        public void RestorePlugin(BackupRecord record) {
            if (record == null) return;
            string pluginBackup = Path.Combine(record.DirectoryPath, "plugin");
            if (!Directory.Exists(pluginBackup)) return;
            if (Directory.Exists(paths.PluginDirectory)) throw new IOException("Plugin destination must be empty before restore.");
            CopyDirectory(pluginBackup, paths.PluginDirectory);
        }

        public static IList<FileHashRecord> HashDirectory(string directory) {
            List<FileHashRecord> result = new List<FileHashRecord>();
            if (!Directory.Exists(directory)) return result;
            string root = EnsureSeparator(Path.GetFullPath(directory));
            IList<string> files = EnumerateFilesSafe(directory, null);
            foreach (string file in files) {
                string full = Path.GetFullPath(file);
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("File escaped hash root.");
                result.Add(new FileHashRecord {
                    RelativePath = full.Substring(root.Length).Replace('\\', '/'),
                    Sha256 = HashFile(full)
                });
            }
            return result;
        }

        public static string HashFile(string path) {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder text = new StringBuilder(64);
                foreach (byte value in hash) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        public static void CopyDirectory(string source, string destination) {
            string sourceRoot = Path.GetFullPath(source);
            DirectoryInfo info = new DirectoryInfo(sourceRoot);
            if (!info.Exists) throw new DirectoryNotFoundException(sourceRoot);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse-point plugin roots are not supported.");
            Directory.CreateDirectory(destination);
            string sourcePrefix = EnsureSeparator(sourceRoot);
            List<string> directories = new List<string>();
            IList<string> files = EnumerateFilesSafe(sourceRoot, directories);
            foreach (string directory in directories) {
                string relative = Path.GetFullPath(directory).Substring(sourcePrefix.Length);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in files) {
                string relative = Path.GetFullPath(file).Substring(sourcePrefix.Length);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private void WriteManifest(BackupRecord record) {
            List<object> files = new List<object>();
            foreach (FileHashRecord file in record.Files) {
                files.Add(ToolCatalog.D("relativePath", file.RelativePath, "sha256", file.Sha256));
            }
            IDictionary<string, object> manifest = ToolCatalog.D(
                "timestamp", record.CreatedAt.ToUniversalTime().ToString("o"),
                "originalPluginVersion", record.OriginalPluginVersion,
                "originalMarketplaceIndex", record.OriginalMarketplaceIndex,
                "originalMarketplaceEntry", record.OriginalMarketplaceEntry,
                "files", files.ToArray());
            File.WriteAllText(Path.Combine(record.DirectoryPath, "manifest.json"), codec.Serialize(manifest), new UTF8Encoding(false));
        }

        private static string ReadPluginVersion(string pluginDirectory) {
            string manifest = Path.Combine(pluginDirectory, ".codex-plugin", "plugin.json");
            if (!File.Exists(manifest)) return null;
            try {
                IDictionary<string, object> value = new JsonCodec().ParseObject(File.ReadAllText(manifest, Encoding.UTF8));
                return StringValue(value, "version");
            } catch { return null; }
        }

        private static string ReadText(string path) { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : ""; }

        private void SafeDeleteBackup(string directory) {
            string root = EnsureSeparator(Path.GetFullPath(Path.Combine(paths.DataDirectory, "backups")));
            string full = Path.GetFullPath(directory);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refusing to delete outside backup root.");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        private static string EnsureSeparator(string path) {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path : path + Path.DirectorySeparatorChar;
        }

        private static IList<string> EnumerateFilesSafe(string root, IList<string> discoveredDirectories) {
            List<string> files = new List<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(Path.GetFullPath(root));
            while (pending.Count > 0) {
                string current = pending.Pop();
                DirectoryInfo currentInfo = new DirectoryInfo(current);
                if ((currentInfo.Attributes & FileAttributes.ReparsePoint) != 0) {
                    throw new InvalidDataException("Reparse points are not supported in plugins.");
                }
                string[] childFiles = Directory.GetFiles(current);
                Array.Sort(childFiles, StringComparer.OrdinalIgnoreCase);
                foreach (string file in childFiles) {
                    FileInfo fileInfo = new FileInfo(file);
                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidDataException("Reparse points are not supported in plugins.");
                    }
                    files.Add(file);
                }
                string[] childDirectories = Directory.GetDirectories(current);
                Array.Sort(childDirectories, StringComparer.OrdinalIgnoreCase);
                for (int index = childDirectories.Length - 1; index >= 0; index--) {
                    DirectoryInfo directoryInfo = new DirectoryInfo(childDirectories[index]);
                    if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidDataException("Reparse points are not supported in plugins.");
                    }
                    if (discoveredDirectories != null) discoveredDirectories.Add(childDirectories[index]);
                    pending.Push(childDirectories[index]);
                }
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            if (discoveredDirectories != null) {
                List<string> ordered = discoveredDirectories as List<string>;
                if (ordered != null) ordered.Sort(StringComparer.OrdinalIgnoreCase);
            }
            return files;
        }

        private static string StringValue(IDictionary<string, object> values, string key) {
            object value;
            return values != null && values.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null;
        }

        private static int IntValue(IDictionary<string, object> values, string key, int defaultValue) {
            object value;
            return values != null && values.TryGetValue(key, out value) && value != null ? Convert.ToInt32(value) : defaultValue;
        }

        private static IDictionary<string, object> ObjectValue(IDictionary<string, object> values, string key) {
            object value;
            return values != null && values.TryGetValue(key, out value) ? value as IDictionary<string, object> : null;
        }

        private static DateTime DateValue(IDictionary<string, object> values, string key) {
            DateTime result;
            return DateTime.TryParse(StringValue(values, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)
                ? result.ToUniversalTime() : default(DateTime);
        }
    }
}
