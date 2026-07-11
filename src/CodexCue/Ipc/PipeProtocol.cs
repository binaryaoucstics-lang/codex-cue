using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodexCue.Core;
using CodexCue.Mcp;

namespace CodexCue.Ipc {
    public sealed class PipeEnvelope {
        public int ProtocolVersion { get; set; }
        public string Type { get; set; }
        public string SessionId { get; set; }
        public IDictionary<string, object> Payload { get; set; }
    }

    public static class PipeProtocol {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly JsonCodec Codec = new JsonCodec();

        public static async Task WriteAsync(Stream stream, PipeEnvelope envelope, CancellationToken cancellationToken) {
            if (stream == null) throw new ArgumentNullException("stream");
            if (envelope == null) throw new ArgumentNullException("envelope");
            string json = Codec.Serialize(ToolCatalog.D(
                "protocolVersion", envelope.ProtocolVersion,
                "type", envelope.Type,
                "sessionId", envelope.SessionId,
                "payload", envelope.Payload ?? ToolCatalog.D()));
            byte[] payload = Utf8.GetBytes(json);
            if (payload.Length > RequestValidator.MaximumMessageBytes) {
                throw new InvalidDataException("IPC message exceeds 1 MiB.");
            }
            byte[] prefix = Prefix(payload.Length);
            await stream.WriteAsync(prefix, 0, prefix.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public static async Task<PipeEnvelope> ReadAsync(Stream stream, CancellationToken cancellationToken) {
            if (stream == null) throw new ArgumentNullException("stream");
            byte[] prefix = await ReadExactlyAsync(stream, 4, cancellationToken).ConfigureAwait(false);
            int length = prefix[0] | (prefix[1] << 8) | (prefix[2] << 16) | (prefix[3] << 24);
            if (length < 0 || length > RequestValidator.MaximumMessageBytes) {
                throw new InvalidDataException("IPC message exceeds 1 MiB.");
            }
            byte[] payload = await ReadExactlyAsync(stream, length, cancellationToken).ConfigureAwait(false);
            IDictionary<string, object> value = Codec.ParseObject(Utf8.GetString(payload));
            return new PipeEnvelope {
                ProtocolVersion = Int(value, "protocolVersion"),
                Type = String(value, "type"),
                SessionId = String(value, "sessionId"),
                Payload = Object(value, "payload") ?? new Dictionary<string, object>()
            };
        }

        public static IDictionary<string, object> RequestPayload(OptionRequest request) {
            List<object> questions = new List<object>();
            foreach (OptionQuestion question in request.Questions) {
                List<object> options = new List<object>();
                foreach (OptionChoice option in question.Options) {
                    options.Add(ToolCatalog.D(
                        "id", option.Id,
                        "label", option.Label,
                        "description", option.Description,
                        "recommended", option.Recommended));
                }
                questions.Add(ToolCatalog.D(
                    "id", question.Id,
                    "prompt", question.Prompt,
                    "description", question.Description,
                    "mode", question.Mode == SelectionMode.Multiple ? "multiple" : "single",
                    "required", question.Required,
                    "allowOther", question.AllowOther,
                    "otherLabel", question.OtherLabel,
                    "options", options.ToArray()));
            }
            IDictionary<string, object> payload = ToolCatalog.D(
                "sessionId", request.SessionId,
                "title", request.Title,
                "cancelLabel", request.CancelLabel,
                "cancelResult", request.CancelResult,
                "questions", questions.ToArray(),
                "reviewMode", Review(request.ReviewMode),
                "maxWaitMs", request.MaxWaitMs,
                "isLegacy", request.IsLegacy,
                "suppressUi", request.SuppressUi,
                "compatibilityWarnings", ToObjects(request.CompatibilityWarnings),
                "createdAt", request.CreatedAt.ToUniversalTime().ToString("o"));
            if (request.AutoResolutionMs.HasValue) payload["autoResolutionMs"] = request.AutoResolutionMs.Value;
            return payload;
        }

        public static OptionRequest RequestFromPayload(IDictionary<string, object> payload) {
            OptionRequest request = RequestNormalizer.Normalize(payload);
            object legacy;
            if (payload.TryGetValue("isLegacy", out legacy) && legacy != null) request.IsLegacy = Convert.ToBoolean(legacy);
            request.SuppressUi = Bool(payload, "suppressUi") || request.SuppressUi;
            object warnings;
            if (payload.TryGetValue("compatibilityWarnings", out warnings)) {
                request.CompatibilityWarnings = Strings(warnings);
            }
            object created;
            DateTime createdAt;
            if (payload.TryGetValue("createdAt", out created) && created != null &&
                DateTime.TryParse(Convert.ToString(created), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out createdAt)) {
                request.CreatedAt = createdAt.ToUniversalTime();
            }
            return request;
        }

        public static IDictionary<string, object> ResultPayload(OptionResult result) {
            List<object> answers = new List<object>();
            foreach (QuestionAnswer answer in result.Answers ?? new List<QuestionAnswer>()) {
                answers.Add(ToolCatalog.D(
                    "questionId", answer.QuestionId,
                    "selectedOptionIds", ToObjects(answer.SelectedOptionIds),
                    "otherText", answer.OtherText));
            }
            IDictionary<string, object> payload = ToolCatalog.D(
                "status", result.Status,
                "sessionId", result.SessionId,
                "answers", answers.ToArray(),
                "source", result.Source,
                "resolution", result.Resolution,
                "protocolVersion", result.ProtocolVersion,
                "createdAt", Date(result.CreatedAt),
                "resolvedAt", Date(result.ResolvedAt),
                "compatibilityWarnings", ToObjects(result.CompatibilityWarnings));
            if (result.SelectedOptionId != null) payload["selectedOptionId"] = result.SelectedOptionId;
            if (result.SelectedOption != null) {
                payload["selectedOption"] = ToolCatalog.D(
                    "id", result.SelectedOption.Id,
                    "label", result.SelectedOption.Label,
                    "description", result.SelectedOption.Description,
                    "recommended", result.SelectedOption.Recommended);
            }
            return payload;
        }

        public static OptionResult ResultFromPayload(IDictionary<string, object> payload) {
            OptionResult result = new OptionResult {
                Status = String(payload, "status"),
                SessionId = String(payload, "sessionId"),
                Source = String(payload, "source"),
                Resolution = String(payload, "resolution"),
                ProtocolVersion = Int(payload, "protocolVersion"),
                CreatedAt = ParseDate(payload, "createdAt"),
                ResolvedAt = ParseDate(payload, "resolvedAt"),
                SelectedOptionId = String(payload, "selectedOptionId")
            };
            object answerValues;
            object[] answerArray = payload.TryGetValue("answers", out answerValues) ? answerValues as object[] : null;
            if (answerArray != null) {
                foreach (object answerValue in answerArray) {
                    IDictionary<string, object> answerObject = answerValue as IDictionary<string, object>;
                    if (answerObject == null) continue;
                    QuestionAnswer answer = new QuestionAnswer {
                        QuestionId = String(answerObject, "questionId"),
                        OtherText = String(answerObject, "otherText"),
                        SelectedOptionIds = Strings(Value(answerObject, "selectedOptionIds"))
                    };
                    result.Answers.Add(answer);
                }
            }
            result.CompatibilityWarnings = Strings(Value(payload, "compatibilityWarnings"));
            IDictionary<string, object> selected = Object(payload, "selectedOption");
            if (selected != null) {
                result.SelectedOption = new OptionChoice {
                    Id = String(selected, "id"),
                    Label = String(selected, "label"),
                    Description = String(selected, "description"),
                    Recommended = Bool(selected, "recommended")
                };
            }
            return result;
        }

        public static IDictionary<string, object> StatusPayload(PromptHostStatus status) {
            return ToolCatalog.D(
                "applicationVersion", status.ApplicationVersion,
                "protocolVersion", status.ProtocolVersion,
                "isRunning", status.IsRunning,
                "activeCount", status.ActiveCount,
                "queuedCount", status.QueuedCount);
        }

        public static PromptHostStatus StatusFromPayload(IDictionary<string, object> payload) {
            return new PromptHostStatus {
                ApplicationVersion = String(payload, "applicationVersion"),
                ProtocolVersion = Int(payload, "protocolVersion"),
                IsRunning = Bool(payload, "isRunning"),
                ActiveCount = Int(payload, "activeCount"),
                QueuedCount = Int(payload, "queuedCount")
            };
        }

        public static PipeEnvelope Envelope(int protocolVersion, string type, string sessionId, IDictionary<string, object> payload) {
            return new PipeEnvelope { ProtocolVersion = protocolVersion, Type = type, SessionId = sessionId, Payload = payload };
        }

        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken cancellationToken) {
            byte[] result = new byte[count];
            int offset = 0;
            while (offset < count) {
                int read = await stream.ReadAsync(result, offset, count - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Unexpected end of IPC message.");
                offset += read;
            }
            return result;
        }

        private static byte[] Prefix(int length) {
            return new byte[] {
                (byte)(length & 0xff), (byte)((length >> 8) & 0xff),
                (byte)((length >> 16) & 0xff), (byte)((length >> 24) & 0xff)
            };
        }

        private static string Review(ReviewMode mode) {
            if (mode == ReviewMode.Always) return "always";
            if (mode == ReviewMode.Never) return "never";
            return "auto";
        }

        private static object Date(DateTime value) {
            return value == default(DateTime) ? null : (object)value.ToUniversalTime().ToString("o");
        }

        private static DateTime ParseDate(IDictionary<string, object> values, string key) {
            DateTime result;
            string value = String(values, key);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result)
                ? result.ToUniversalTime() : default(DateTime);
        }

        private static object[] ToObjects(IEnumerable<string> values) {
            if (values == null) return new object[0];
            List<object> result = new List<object>();
            foreach (string value in values) result.Add(value);
            return result.ToArray();
        }

        private static IList<string> Strings(object value) {
            List<string> result = new List<string>();
            object[] values = value as object[];
            if (values != null) foreach (object item in values) result.Add(Convert.ToString(item));
            return result;
        }

        private static object Value(IDictionary<string, object> values, string key) {
            object value;
            return values != null && values.TryGetValue(key, out value) ? value : null;
        }

        private static string String(IDictionary<string, object> values, string key) {
            object value = Value(values, key);
            return value == null ? null : Convert.ToString(value);
        }

        private static int Int(IDictionary<string, object> values, string key) {
            object value = Value(values, key);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static bool Bool(IDictionary<string, object> values, string key) {
            object value = Value(values, key);
            return value != null && Convert.ToBoolean(value);
        }

        private static IDictionary<string, object> Object(IDictionary<string, object> values, string key) {
            return Value(values, key) as IDictionary<string, object>;
        }
    }
}
