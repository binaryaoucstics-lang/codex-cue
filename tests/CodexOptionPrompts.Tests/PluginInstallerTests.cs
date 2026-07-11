using System.IO;
using CodexOptionPrompts.Install;

namespace CodexOptionPrompts.Tests {
    internal static class PluginInstallerTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Installer failed replacement restores original plugin", delegate {
                using (TemporaryHome home = TemporaryHome.WithPrototype()) {
                    Assert.Throws<InstallException>(delegate { InstallFixtures.FailAfterBackup(home); });
                    Assert.True(home.PrototypeFilesAreIntact());
                    Assert.True(home.PrototypeMarketplaceEntryIsIntact());
                    Assert.False(Directory.Exists(home.Paths.PluginDirectory + ".new"));
                }
            });

            tests.Add("Installer successful replacement writes managed hashes and status", delegate {
                using (TemporaryHome home = TemporaryHome.WithPrototype()) {
                    PluginInstaller.ForTest(home.Path).Install(home.CreateInstallRequest());
                    Assert.True(File.Exists(Path.Combine(home.Paths.PluginDirectory, ".codex-plugin", "plugin.json")));
                    Assert.True(File.Exists(Path.Combine(home.Paths.DataDirectory, "managed-manifest.json")));
                    Assert.True(File.Exists(Path.Combine(home.Paths.DataDirectory, "install-status.json")));
                    string config = File.ReadAllText(home.Paths.CodexConfigPath);
                    Assert.True(config.Contains("default_tools_approval_mode = \"approve\""));
                    Assert.True(home.BackupContainsPrototype());
                    Assert.True(home.InstallStatusContainsOnlySafeFields());
                }
            });

            tests.Add("Installer uninstall preserves user modified managed file", delegate {
                using (TemporaryHome home = TemporaryHome.WithInstalledProduct()) {
                    home.ModifyInstalledSkill();
                    PluginInstaller.ForTest(home.Path).Uninstall(new UninstallRequest { RestoreBackup = true });
                    Assert.True(home.ModifiedSkillStillExists());
                }
            });

            tests.Add("Installer uninstall restores unmodified prototype", delegate {
                using (TemporaryHome home = TemporaryHome.WithInstalledProduct()) {
                    PluginInstaller.ForTest(home.Path).Uninstall(new UninstallRequest { RestoreBackup = true });
                    Assert.True(home.PrototypeFilesAreIntact());
                }
            });

            tests.Add("Installer malformed staged manifest returns stable install error", delegate {
                using (TemporaryHome home = TemporaryHome.WithPrototype()) {
                    home.CorruptStagedManifest();
                    InstallException error = Assert.Throws<InstallException>(delegate {
                        PluginInstaller.ForTest(home.Path).Install(home.CreateInstallRequest());
                    });
                    Assert.Equal("INSTALLATION_INCOMPLETE", error.Code);
                    Assert.True(home.PrototypeFilesAreIntact());
                }
            });
        }
    }
}
