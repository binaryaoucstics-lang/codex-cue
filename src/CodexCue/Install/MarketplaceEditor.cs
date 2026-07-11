using System;
using System.Collections.Generic;
using CodexCue.Mcp;

namespace CodexCue.Install {
    public static class MarketplaceEditor {
        private static readonly JsonCodec Codec = new JsonCodec();

        public static string Upsert(string json, MarketplacePlugin entry) {
            if (entry == null || String.IsNullOrWhiteSpace(entry.Name)) throw new ArgumentException("A marketplace entry is required.", "entry");
            IDictionary<string, object> root = ParseRoot(json);
            List<object> plugins = Plugins(root);
            IDictionary<string, object> managed = ToDictionary(entry);
            int index = FindIndex(plugins, entry.Name);
            if (index >= 0) plugins[index] = managed;
            else plugins.Add(managed);
            root["plugins"] = plugins.ToArray();
            return Codec.Serialize(root);
        }

        public static string RemoveManaged(string json, string name) {
            IDictionary<string, object> root = ParseRoot(json);
            List<object> plugins = Plugins(root);
            int index = FindIndex(plugins, name);
            if (index >= 0) plugins.RemoveAt(index);
            root["plugins"] = plugins.ToArray();
            return Codec.Serialize(root);
        }

        public static string RestoreEntry(string json, string name, IDictionary<string, object> originalEntry, int originalIndex) {
            IDictionary<string, object> root = ParseRoot(json);
            List<object> plugins = Plugins(root);
            int current = FindIndex(plugins, name);
            if (current >= 0) plugins.RemoveAt(current);
            if (originalEntry != null) {
                int index = Math.Max(0, Math.Min(originalIndex, plugins.Count));
                plugins.Insert(index, originalEntry);
            }
            root["plugins"] = plugins.ToArray();
            return Codec.Serialize(root);
        }

        public static IDictionary<string, object> FindEntry(string json, string name, out int index) {
            IDictionary<string, object> root = ParseRoot(json);
            List<object> plugins = Plugins(root);
            index = FindIndex(plugins, name);
            return index < 0 ? null : plugins[index] as IDictionary<string, object>;
        }

        public static string MarketplaceName(string json) {
            IDictionary<string, object> root = ParseRoot(json);
            object value;
            return root.TryGetValue("name", out value) && value != null ? Convert.ToString(value) : "personal-plugins";
        }

        private static IDictionary<string, object> ParseRoot(string json) {
            if (String.IsNullOrWhiteSpace(json)) {
                return new Dictionary<string, object> {
                    { "name", "personal-plugins" },
                    { "interface", new Dictionary<string, object> { { "displayName", "Personal Plugins" } } },
                    { "plugins", new object[0] }
                };
            }
            return Codec.ParseObject(json);
        }

        private static List<object> Plugins(IDictionary<string, object> root) {
            object value;
            object[] items = root.TryGetValue("plugins", out value) ? value as object[] : null;
            return items == null ? new List<object>() : new List<object>(items);
        }

        private static int FindIndex(IList<object> plugins, string name) {
            for (int index = 0; index < plugins.Count; index++) {
                IDictionary<string, object> plugin = plugins[index] as IDictionary<string, object>;
                object value;
                if (plugin != null && plugin.TryGetValue("name", out value) &&
                    String.Equals(Convert.ToString(value), name, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private static IDictionary<string, object> ToDictionary(MarketplacePlugin entry) {
            return ToolCatalog.D(
                "name", entry.Name,
                "source", ToolCatalog.D("source", "local", "path", entry.SourcePath),
                "policy", ToolCatalog.D(
                    "installation", entry.InstallationPolicy,
                    "authentication", entry.AuthenticationPolicy),
                "category", entry.Category);
        }
    }
}
