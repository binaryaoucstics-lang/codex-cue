using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CodexCue.Mcp;

namespace CodexCue.Install {
    public class PluginInstaller {
        private const string ManagedName = "codex-cue";
        private const string LegacyManagedName = "codex-option-prompts";
        private readonly InstallPaths paths;
        private readonly JsonCodec codec = new JsonCodec();

        protected PluginInstaller(InstallPaths paths) {
            if (paths == null) throw new ArgumentNullException("paths");
            this.paths = paths;
        }

        public static PluginInstaller ForCurrentUser() { return new PluginInstaller(InstallPaths.Current()); }
        public static PluginInstaller ForTest(string home) { return new PluginInstaller(InstallPaths.ForHome(home)); }

        public void Install(InstallRequest request) {
            try { InstallCore(request); }
            catch (InstallException) { throw; }
            catch (Exception error) {
                throw new InstallException("INSTALLATION_INCOMPLETE", "The plugin installation could not be completed.", error);
            }
        }

        private void InstallCore(InstallRequest request) {
            ValidateRequest(request);
            MigrateLegacyDirectories();
            Directory.CreateDirectory(paths.DataDirectory);
            InstallProgram(request.StagedProgramPath);

            BackupRecord backup;
            try { backup = new BackupManager(paths).Create(); }
            catch (Exception error) { throw new InstallException("BACKUP_FAILED", "The existing plugin could not be backed up.", error); }

            string staged = paths.PluginDirectory + ".new";
            string previous = paths.PluginDirectory + ".old";
            RecoverInterruptedTransaction(previous);
            SafeDeleteTransactionDirectory(staged, ".new");

            string originalMarketplace = ReadText(paths.MarketplacePath);
            bool originalConfigExisted = File.Exists(paths.CodexConfigPath);
            string originalConfig = ReadText(paths.CodexConfigPath);
            bool movedPrevious = false;
            bool activated = false;
            try {
                BackupManager.CopyDirectory(request.StagedPluginPath, staged);
                VerifyCopy(request.StagedPluginPath, staged);
                if (Directory.Exists(paths.PluginDirectory)) {
                    Directory.Move(paths.PluginDirectory, previous);
                    movedPrevious = true;
                }
                ActivateStagedPlugin(staged, paths.PluginDirectory);
                activated = true;

                MarketplacePlugin entry = ManagedEntry();
                string withoutLegacy = MarketplaceEditor.RemoveManaged(originalMarketplace, LegacyManagedName);
                string marketplace = MarketplaceEditor.Upsert(withoutLegacy, entry);
                WriteAtomic(paths.MarketplacePath, marketplace);
                WriteManagedManifest(backup);
                WriteInstallStatus(request, marketplace);
                WriteAtomic(paths.CodexConfigPath, CodexConfigEditor.EnableDirectQuestions(ReadText(paths.CodexConfigPath)));

                if (Directory.Exists(previous)) SafeDeleteTransactionDirectory(previous, ".old");
            } catch (Exception error) {
                try {
                    if (activated && Directory.Exists(paths.PluginDirectory)) SafeDeletePluginDirectory(paths.PluginDirectory);
                    if (movedPrevious && Directory.Exists(previous) && !Directory.Exists(paths.PluginDirectory)) {
                        Directory.Move(previous, paths.PluginDirectory);
                    }
                    if (Directory.Exists(staged)) SafeDeleteTransactionDirectory(staged, ".new");
                    RestoreMarketplace(originalMarketplace);
                    RestoreText(paths.CodexConfigPath, originalConfigExisted, originalConfig);
                    DeleteIfExists(Path.Combine(paths.DataDirectory, "managed-manifest.json"));
                    DeleteIfExists(Path.Combine(paths.DataDirectory, "install-status.json"));
                } catch (Exception rollbackError) {
                    throw new InstallException("ROLLBACK_FAILED", "Plugin activation failed and rollback was incomplete.", rollbackError);
                }
                throw new InstallException("INSTALLATION_INCOMPLETE", "Plugin activation failed; the previous plugin was restored.", error);
            }
        }

        public void Uninstall(UninstallRequest request) {
            try { UninstallCore(request); }
            catch (InstallException) { throw; }
            catch (Exception error) {
                throw new InstallException("UNINSTALL_INCOMPLETE", "The plugin uninstall could not be completed safely.", error);
            }
        }

        private void UninstallCore(UninstallRequest request) {
            if (request == null) throw new ArgumentNullException("request");
            string manifestPath = Path.Combine(paths.DataDirectory, "managed-manifest.json");
            if (!File.Exists(manifestPath)) return;
            ManagedInstall managed = ReadManagedManifest(manifestPath);
            if (HasUserChanges(managed)) return;

            if (Directory.Exists(paths.PluginDirectory)) SafeDeletePluginDirectory(paths.PluginDirectory);
            BackupRecord backup = null;
            if (request.RestoreBackup && !String.IsNullOrWhiteSpace(managed.BackupDirectory) && Directory.Exists(managed.BackupDirectory)) {
                backup = new BackupManager(paths).Read(managed.BackupDirectory);
                new BackupManager(paths).RestorePlugin(backup);
            }

            string marketplace = ReadText(paths.MarketplacePath);
            marketplace = backup == null
                ? MarketplaceEditor.RemoveManaged(marketplace, ManagedName)
                : MarketplaceEditor.RestoreEntry(marketplace, ManagedName, backup.OriginalMarketplaceEntry, backup.OriginalMarketplaceIndex);
            WriteAtomic(paths.MarketplacePath, marketplace);
            DeleteIfExists(manifestPath);
            DeleteIfExists(Path.Combine(paths.DataDirectory, "install-status.json"));
        }

        protected virtual void ActivateStagedPlugin(string stagedDirectory, string destinationDirectory) {
            Directory.Move(stagedDirectory, destinationDirectory);
        }

        private void ValidateRequest(InstallRequest request) {
            if (request == null) throw new ArgumentNullException("request");
            if (String.IsNullOrWhiteSpace(request.StagedProgramPath) || !File.Exists(request.StagedProgramPath)) {
                throw new InstallException("INSTALLATION_INCOMPLETE", "The staged program is missing.");
            }
            if (String.IsNullOrWhiteSpace(request.StagedPluginPath) || !Directory.Exists(request.StagedPluginPath)) {
                throw new InstallException("INSTALLATION_INCOMPLETE", "The staged plugin is missing.");
            }
            string manifest = Path.Combine(request.StagedPluginPath, ".codex-plugin", "plugin.json");
            if (!File.Exists(manifest)) throw new InstallException("INSTALLATION_INCOMPLETE", "The staged plugin manifest is missing.");
            IDictionary<string, object> value = codec.ParseObject(File.ReadAllText(manifest, Encoding.UTF8));
            object name;
            if (!value.TryGetValue("name", out name) || !String.Equals(Convert.ToString(name), ManagedName, StringComparison.Ordinal)) {
                throw new InstallException("INSTALLATION_INCOMPLETE", "The staged plugin name is invalid.");
            }
        }

        private void InstallProgram(string stagedProgram) {
            Directory.CreateDirectory(paths.ProgramDirectory);
            string destination = Path.Combine(paths.ProgramDirectory, "CodexCue.exe");
            string source = Path.GetFullPath(stagedProgram);
            if (String.Equals(source, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
            string temporary = destination + ".new";
            File.Copy(source, temporary, true);
            if (!String.Equals(BackupManager.HashFile(source), BackupManager.HashFile(temporary), StringComparison.Ordinal)) {
                DeleteIfExists(temporary);
                throw new InstallException("INSTALLATION_INCOMPLETE", "The staged program failed hash validation.");
            }
            if (File.Exists(destination)) {
                string old = destination + ".old";
                DeleteIfExists(old);
                File.Replace(temporary, destination, old, true);
                DeleteIfExists(old);
            } else File.Move(temporary, destination);
        }

        private static void VerifyCopy(string source, string destination) {
            IList<FileHashRecord> expected = BackupManager.HashDirectory(source);
            IList<FileHashRecord> actual = BackupManager.HashDirectory(destination);
            if (expected.Count != actual.Count) throw new InvalidDataException("Staged plugin file count changed during copy.");
            for (int index = 0; index < expected.Count; index++) {
                if (!String.Equals(expected[index].RelativePath, actual[index].RelativePath, StringComparison.Ordinal) ||
                    !String.Equals(expected[index].Sha256, actual[index].Sha256, StringComparison.Ordinal)) {
                    throw new InvalidDataException("Staged plugin hash validation failed.");
                }
            }
        }

        private void WriteManagedManifest(BackupRecord backup) {
            IList<FileHashRecord> hashes = BackupManager.HashDirectory(paths.PluginDirectory);
            List<object> files = new List<object>();
            foreach (FileHashRecord hash in hashes) {
                files.Add(ToolCatalog.D("relativePath", hash.RelativePath, "sha256", hash.Sha256));
            }
            IDictionary<string, object> manifest = ToolCatalog.D(
                "version", "1",
                "pluginName", ManagedName,
                "pluginDirectory", paths.PluginDirectory,
                "backupDirectory", backup.DirectoryPath,
                "files", files.ToArray(),
                "installedAt", DateTime.UtcNow.ToString("o"));
            WriteAtomic(Path.Combine(paths.DataDirectory, "managed-manifest.json"), codec.Serialize(manifest));
        }

        private void WriteInstallStatus(InstallRequest request, string marketplaceJson) {
            string codex = null;
            object refreshExit = null;
            object installExit = null;
            if (request.RefreshCodex) {
                CodexLocator locator = new CodexLocator();
                codex = locator.Find(null);
                if (codex != null) {
                    string marketplace = MarketplaceEditor.MarketplaceName(marketplaceJson);
                    refreshExit = locator.RefreshMarketplace(codex, marketplace);
                    installExit = locator.InstallPlugin(codex, ManagedName, marketplace);
                }
            }
            IDictionary<string, object> status = ToolCatalog.D(
                "productVersion", InstalledPluginVersion(),
                "codexCli", codex,
                "marketplaceName", MarketplaceEditor.MarketplaceName(marketplaceJson),
                "refreshExitCode", refreshExit,
                "pluginInstallExitCode", installExit,
                "timestamp", DateTime.UtcNow.ToString("o"));
            WriteAtomic(Path.Combine(paths.DataDirectory, "install-status.json"), codec.Serialize(status));
        }

        private string InstalledPluginVersion() {
            string manifest = Path.Combine(paths.PluginDirectory, ".codex-plugin", "plugin.json");
            IDictionary<string, object> value = codec.ParseObject(File.ReadAllText(manifest, Encoding.UTF8));
            object version;
            return value.TryGetValue("version", out version) ? Convert.ToString(version) : "2.2.0";
        }

        private ManagedInstall ReadManagedManifest(string path) {
            IDictionary<string, object> value = codec.ParseObject(File.ReadAllText(path, Encoding.UTF8));
            ManagedInstall result = new ManagedInstall();
            object backup;
            if (value.TryGetValue("backupDirectory", out backup)) result.BackupDirectory = Convert.ToString(backup);
            object filesValue;
            object[] files = value.TryGetValue("files", out filesValue) ? filesValue as object[] : null;
            if (files != null) {
                foreach (object itemValue in files) {
                    IDictionary<string, object> item = itemValue as IDictionary<string, object>;
                    object relative;
                    object hash;
                    if (item != null && item.TryGetValue("relativePath", out relative) && item.TryGetValue("sha256", out hash)) {
                        result.Files.Add(new FileHashRecord { RelativePath = Convert.ToString(relative), Sha256 = Convert.ToString(hash) });
                    }
                }
            }
            return result;
        }

        private bool HasUserChanges(ManagedInstall managed) {
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileHashRecord file in managed.Files) expected[file.RelativePath] = file.Sha256;
            if (!Directory.Exists(paths.PluginDirectory)) return false;
            foreach (FileHashRecord current in BackupManager.HashDirectory(paths.PluginDirectory)) {
                string hash;
                if (!expected.TryGetValue(current.RelativePath, out hash) || !String.Equals(hash, current.Sha256, StringComparison.Ordinal)) return true;
                expected.Remove(current.RelativePath);
            }
            return false;
        }

        private void RecoverInterruptedTransaction(string previous) {
            if (!Directory.Exists(previous)) return;
            if (Directory.Exists(paths.PluginDirectory)) {
                throw new InstallException("INSTALLATION_INCOMPLETE", "A previous plugin transaction requires manual recovery.");
            }
            Directory.Move(previous, paths.PluginDirectory);
        }

        private void RestoreMarketplace(string original) {
            if (String.IsNullOrEmpty(original)) DeleteIfExists(paths.MarketplacePath);
            else WriteAtomic(paths.MarketplacePath, original);
        }

        private static void RestoreText(string path, bool existed, string original) {
            if (!existed) DeleteIfExists(path);
            else WriteAtomic(path, original ?? "");
        }

        private static MarketplacePlugin ManagedEntry() {
            return new MarketplacePlugin {
                Name = ManagedName,
                SourcePath = "./plugins/codex-cue",
                InstallationPolicy = "AVAILABLE",
                AuthenticationPolicy = "ON_INSTALL",
                Category = "Productivity"
            };
        }

        private void MigrateLegacyDirectories() {
            if (!Directory.Exists(paths.PluginDirectory) && Directory.Exists(paths.LegacyPluginDirectory)) {
                Directory.CreateDirectory(Path.GetDirectoryName(paths.PluginDirectory));
                Directory.Move(paths.LegacyPluginDirectory, paths.PluginDirectory);
            }
            if (!Directory.Exists(paths.DataDirectory) && Directory.Exists(paths.LegacyDataDirectory)) {
                Directory.CreateDirectory(Path.GetDirectoryName(paths.DataDirectory));
                Directory.Move(paths.LegacyDataDirectory, paths.DataDirectory);
            }
        }

        private static string ReadText(string path) { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : ""; }

        private static void WriteAtomic(string path, string text) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".new";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null, true);
            else File.Move(temporary, path);
        }

        private void SafeDeletePluginDirectory(string directory) {
            string full = Path.GetFullPath(directory);
            if (!String.Equals(full, Path.GetFullPath(paths.PluginDirectory), StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Refusing to delete an unexpected plugin path.");
            }
            Directory.Delete(full, true);
        }

        private void SafeDeleteTransactionDirectory(string directory, string suffix) {
            string full = Path.GetFullPath(directory);
            string expected = Path.GetFullPath(paths.PluginDirectory + suffix);
            if (!String.Equals(full, expected, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Refusing to delete an unexpected transaction path.");
            }
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }

        private sealed class ManagedInstall {
            public ManagedInstall() { Files = new List<FileHashRecord>(); }
            public string BackupDirectory { get; set; }
            public IList<FileHashRecord> Files { get; private set; }
        }
    }
}
