using System.IO;
using CodexOptionPrompts.Install;

namespace CodexOptionPrompts.Tests {
    internal static class BackupManagerTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Installer paths stay below an injected test home", delegate {
                using (TemporaryHome home = TemporaryHome.WithPrototype()) {
                    InstallPaths paths = InstallPaths.ForHome(home.Path);
                    Assert.True(paths.ProgramDirectory.StartsWith(home.Path));
                    Assert.True(paths.PluginDirectory.StartsWith(home.Path));
                    Assert.True(paths.MarketplacePath.StartsWith(home.Path));
                    Assert.True(paths.DataDirectory.StartsWith(home.Path));
                }
            });

            tests.Add("Installer backup records hashes version and marketplace entry", delegate {
                using (TemporaryHome home = TemporaryHome.WithPrototype()) {
                    BackupRecord record = new BackupManager(home.Paths).Create();
                    Assert.True(Directory.Exists(record.DirectoryPath));
                    Assert.True(File.Exists(Path.Combine(record.DirectoryPath, "manifest.json")));
                    Assert.Equal("0.1.0", record.OriginalPluginVersion);
                    Assert.True(record.Files.Count > 0);
                    Assert.True(record.OriginalMarketplaceEntry != null);
                    Assert.True(record.Files[0].Sha256.Length == 64);
                }
            });
        }
    }
}
