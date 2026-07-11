using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexCue.Settings {
    public sealed class CueSettings {
        public bool CompletionSuggestionsEnabled { get; set; }
        public int CompletionOptionCount { get; set; }
        public bool SkipNextCompletion { get; set; }

        public static CueSettings Defaults() {
            return new CueSettings {
                CompletionSuggestionsEnabled = true,
                CompletionOptionCount = 3,
                SkipNextCompletion = false
            };
        }

        public void Normalize() {
            if (CompletionOptionCount < 1) CompletionOptionCount = 1;
            if (CompletionOptionCount > 6) CompletionOptionCount = 6;
        }
    }

    public sealed class CueSettingsStore {
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public CueSettingsStore(string path) {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", "path");
            this.path = Path.GetFullPath(path);
        }

        public static CueSettingsStore Current() {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new CueSettingsStore(Path.Combine(local, "CodexCue", "settings.json"));
        }

        public CueSettings Load() {
            if (!File.Exists(path)) return CueSettings.Defaults();
            try {
                CueSettings value = serializer.Deserialize<CueSettings>(File.ReadAllText(path, Encoding.UTF8));
                if (value == null) return CueSettings.Defaults();
                value.Normalize();
                return value;
            } catch (IOException) { return CueSettings.Defaults(); }
              catch (ArgumentException) { return CueSettings.Defaults(); }
              catch (InvalidOperationException) { return CueSettings.Defaults(); }
        }

        public void Save(CueSettings value) {
            if (value == null) throw new ArgumentNullException("value");
            value.Normalize();
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(value), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        public bool ConsumeSkipNextCompletion() {
            CueSettings value = Load();
            if (!value.SkipNextCompletion) return false;
            value.SkipNextCompletion = false;
            Save(value);
            return true;
        }
    }
}
