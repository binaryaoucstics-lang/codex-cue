using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodexCue.Core;

namespace CodexCue.Mcp {
    public sealed class PromptClientException : Exception {
        public PromptClientException(string code, string message) : base(message) { Code = code; }
        public PromptClientException(string code, string message, Exception inner) : base(message, inner) { Code = code; }
        public string Code { get; private set; }
    }

    public sealed class McpServer {
        public const string ProtocolVersion = "2024-11-05";
        private readonly IPromptClient promptClient;
        private readonly JsonCodec codec;

        public McpServer(IPromptClient promptClient, JsonCodec codec) {
            if (promptClient == null) throw new ArgumentNullException("promptClient");
            if (codec == null) throw new ArgumentNullException("codec");
            this.promptClient = promptClient;
            this.codec = codec;
        }

        public async Task<IDictionary<string, object>> HandleAsync(IDictionary<string, object> request, CancellationToken cancellationToken) {
            object id = null;
            object idValue = null;
            bool hasId = request != null && request.TryGetValue("id", out idValue);
            if (hasId) id = idValue;
            if (request == null) return Error(id, -32600, "Invalid Request", "INVALID_REQUEST");

            object methodValue;
            string method = request.TryGetValue("method", out methodValue) ? methodValue as string : null;
            if (String.IsNullOrEmpty(method)) return hasId ? Error(id, -32600, "Invalid Request", "INVALID_REQUEST") : null;
            if (String.Equals(method, "notifications/initialized", StringComparison.Ordinal)) return null;

            IDictionary<string, object> response;
            if (String.Equals(method, "initialize", StringComparison.Ordinal)) response = Success(id, InitializeResult());
            else if (String.Equals(method, "ping", StringComparison.Ordinal)) response = Success(id, ToolCatalog.D());
            else if (String.Equals(method, "tools/list", StringComparison.Ordinal)) response = Success(id, ToolCatalog.D("tools", ToolCatalog.Tools()));
            else if (String.Equals(method, "resources/list", StringComparison.Ordinal)) response = Success(id, ToolCatalog.D("resources", new object[0]));
            else if (String.Equals(method, "prompts/list", StringComparison.Ordinal)) response = Success(id, ToolCatalog.D("prompts", new object[0]));
            else if (String.Equals(method, "tools/call", StringComparison.Ordinal)) response = await CallToolAsync(id, Parameters(request), cancellationToken).ConfigureAwait(false);
            else response = Error(id, -32601, "Method not found", "METHOD_NOT_FOUND");

            return hasId ? response : null;
        }

        public async Task RunAsync(StdioTransport transport, TextWriter diagnostics, CancellationToken cancellationToken) {
            if (transport == null) throw new ArgumentNullException("transport");
            while (!cancellationToken.IsCancellationRequested) {
                string message;
                try {
                    message = transport.ReadMessage();
                } catch (Exception error) {
                    if (diagnostics != null) diagnostics.WriteLine("MCP read error: " + error.GetType().Name);
                    transport.WriteMessage(codec.Serialize(Error(null, -32700, "Parse error", "INVALID_REQUEST")));
                    continue;
                }
                if (message == null) break;

                IDictionary<string, object> response;
                try {
                    response = await HandleAsync(codec.ParseObject(message), cancellationToken).ConfigureAwait(false);
                } catch (Exception error) {
                    if (diagnostics != null) diagnostics.WriteLine("MCP dispatch error: " + error.GetType().Name);
                    response = Error(null, -32603, "Internal error", "HOST_UNAVAILABLE");
                }
                if (response != null) transport.WriteMessage(codec.Serialize(response));
            }
        }

        private async Task<IDictionary<string, object>> CallToolAsync(object id, IDictionary<string, object> parameters, CancellationToken cancellationToken) {
            object nameValue;
            string name = parameters != null && parameters.TryGetValue("name", out nameValue) ? nameValue as string : null;
            object argumentsValue;
            IDictionary<string, object> arguments = parameters != null && parameters.TryGetValue("arguments", out argumentsValue)
                ? argumentsValue as IDictionary<string, object>
                : new Dictionary<string, object>();
            if (String.IsNullOrEmpty(name) || arguments == null) return Error(id, -32602, "Invalid params", "INVALID_REQUEST");

            try {
                if (String.Equals(name, "ask_options", StringComparison.Ordinal)) {
                    OptionRequest request = RequestNormalizer.Normalize(arguments);
                    ValidationResult validation = RequestValidator.Validate(request);
                    if (!validation.IsValid) return Error(id, -32602, validation.Message, validation.Code);
                    OptionResult result = await promptClient.AskAsync(request, cancellationToken).ConfigureAwait(false);
                    return Success(id, ToolResult(ResultPayload(result)));
                }
                if (String.Equals(name, "option_prompt_status", StringComparison.Ordinal)) {
                    PromptHostStatus status = await promptClient.GetStatusAsync(cancellationToken).ConfigureAwait(false);
                    return Success(id, ToolResult(StatusPayload(status)));
                }
                return Error(id, -32602, "Unknown tool.", "INVALID_REQUEST");
            } catch (PromptClientException error) {
                return Error(id, -32000, error.Message, error.Code);
            } catch (OperationCanceledException) {
                return Error(id, -32000, "Request cancelled.", "REQUEST_CANCELLED");
            } catch (ArgumentException error) {
                return Error(id, -32602, error.Message, "INVALID_REQUEST");
            } catch (FormatException error) {
                return Error(id, -32602, error.Message, "INVALID_REQUEST");
            } catch (InvalidCastException error) {
                return Error(id, -32602, error.Message, "INVALID_REQUEST");
            } catch (Exception) {
                return Error(id, -32000, "Prompt host unavailable.", "HOST_UNAVAILABLE");
            }
        }

        private IDictionary<string, object> InitializeResult() {
            return ToolCatalog.D(
                "protocolVersion", ProtocolVersion,
                "capabilities", ToolCatalog.D("tools", ToolCatalog.D()),
                "serverInfo", ToolCatalog.D("name", "codex-cue", "version", "2.0.0"),
                "instructions", "Call ask_options directly for every question that needs a user response, including open-ended questions. Send all text in MCP JSON. Never use PowerShell, shell commands, clipboards, or files to transport question text.");
        }

        private IDictionary<string, object> ToolResult(IDictionary<string, object> payload) {
            return ToolCatalog.D(
                "content", new object[] { ToolCatalog.D("type", "text", "text", codec.Serialize(payload)) },
                "structuredContent", payload,
                "isError", false);
        }

        private static IDictionary<string, object> ResultPayload(OptionResult result) {
            List<object> answers = new List<object>();
            if (result.Answers != null) {
                foreach (QuestionAnswer answer in result.Answers) {
                    answers.Add(ToolCatalog.D(
                        "questionId", answer.QuestionId,
                        "selectedOptionIds", ToObjects(answer.SelectedOptionIds),
                        "otherText", answer.OtherText));
                }
            }
            IDictionary<string, object> payload = ToolCatalog.D(
                "status", result.Status,
                "sessionId", result.SessionId,
                "answers", answers.ToArray(),
                "source", result.Source,
                "resolution", result.Resolution,
                "protocolVersion", result.ProtocolVersion,
                "createdAt", Iso(result.CreatedAt),
                "resolvedAt", Iso(result.ResolvedAt),
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

        private static IDictionary<string, object> StatusPayload(PromptHostStatus status) {
            return ToolCatalog.D(
                "applicationVersion", status.ApplicationVersion,
                "protocolVersion", status.ProtocolVersion,
                "isRunning", status.IsRunning,
                "activeCount", status.ActiveCount,
                "queuedCount", status.QueuedCount);
        }

        private static object[] ToObjects(IEnumerable<string> values) {
            if (values == null) return new object[0];
            List<object> result = new List<object>();
            foreach (string value in values) result.Add(value);
            return result.ToArray();
        }

        private static object Iso(DateTime value) {
            return value == default(DateTime) ? null : (object)value.ToUniversalTime().ToString("o");
        }

        private static IDictionary<string, object> Parameters(IDictionary<string, object> request) {
            object value;
            if (!request.TryGetValue("params", out value) || value == null) return new Dictionary<string, object>();
            return value as IDictionary<string, object>;
        }

        private static IDictionary<string, object> Success(object id, object result) {
            return ToolCatalog.D("jsonrpc", "2.0", "id", id, "result", result);
        }

        private static IDictionary<string, object> Error(object id, int code, string message, string stableCode) {
            return ToolCatalog.D(
                "jsonrpc", "2.0",
                "id", id,
                "error", ToolCatalog.D(
                    "code", code,
                    "message", message,
                    "data", ToolCatalog.D("code", stableCode)));
        }
    }
}
