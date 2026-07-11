using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexCue.Tests {
    internal static class Fixtures {
        public static string Text(string name) {
            string path = Resolve(name);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public static IDictionary<string, object> Object(string name) {
            object value = new JavaScriptSerializer().DeserializeObject(Text(name));
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null) throw new InvalidDataException("Fixture is not a JSON object: " + name);
            return result;
        }

        private static string Resolve(string name) {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null) {
                string candidate = Path.Combine(directory.FullName, "tests", "fixtures", name);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Fixture not found: " + name, name);
        }
    }
}
