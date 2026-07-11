using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CodexOptionPrompts.Mcp {
    public sealed class JsonCodec {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer {
            MaxJsonLength = 1024 * 1024,
            RecursionLimit = 64
        };

        public IDictionary<string, object> ParseObject(string json) {
            IDictionary<string, object> result = serializer.DeserializeObject(json) as IDictionary<string, object>;
            if (result == null) throw new InvalidDataException("JSON value must be an object.");
            return result;
        }

        public string Serialize(object value) {
            return serializer.Serialize(value);
        }
    }
}
