using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Mcp;

namespace CodexOptionPrompts.Tests {
    internal sealed class FakePromptClient : IPromptClient {
        private OptionResult result;
        private PromptHostStatus status;

        public FakePromptClient() {
            result = new OptionResult { Status = "cancelled", Source = "desktop-wpf", Resolution = "user", ProtocolVersion = 1 };
            status = new PromptHostStatus { ApplicationVersion = "1.0.0", ProtocolVersion = 1, IsRunning = true };
        }

        public static FakePromptClient Submit(string questionId, string optionId) {
            FakePromptClient client = new FakePromptClient();
            QuestionAnswer answer = new QuestionAnswer { QuestionId = questionId };
            answer.SelectedOptionIds.Add(optionId);
            client.result = new OptionResult {
                Status = "submitted",
                Source = "desktop-wpf",
                Resolution = "user",
                ProtocolVersion = 1,
                Answers = new List<QuestionAnswer> { answer }
            };
            return client;
        }

        public static FakePromptClient WithStatus(int active, int queued) {
            FakePromptClient client = new FakePromptClient();
            client.status.ActiveCount = active;
            client.status.QueuedCount = queued;
            return client;
        }

        public Task<OptionResult> AskAsync(OptionRequest request, CancellationToken cancellationToken) {
            return Task.FromResult(result);
        }

        public Task<PromptHostStatus> GetStatusAsync(CancellationToken cancellationToken) {
            return Task.FromResult(status);
        }
    }

    internal static class TransportFixtures {
        public static IList<IDictionary<string, object>> ReadJsonlAndHeaderMessages() {
            StdioTransport transport = new StdioTransport(MixedInput(), new MemoryStream());
            JsonCodec codec = new JsonCodec();
            return new List<IDictionary<string, object>> {
                codec.ParseObject(transport.ReadMessage()),
                codec.ParseObject(transport.ReadMessage())
            };
        }

        public static MemoryStream MixedInput() {
            string first = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}";
            string second = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}";
            byte[] payload = Encoding.UTF8.GetBytes(second);
            string all = first + "\nContent-Length: " + payload.Length + "\r\n\r\n" + second;
            return new MemoryStream(Encoding.UTF8.GetBytes(all));
        }
    }

    internal static class McpFixtures {
        public static IDictionary<string, object> Call(string method, IDictionary<string, object> parameters, IPromptClient client) {
            IDictionary<string, object> request = new Dictionary<string, object> {
                { "jsonrpc", "2.0" }, { "id", 1 }, { "method", method }
            };
            if (parameters != null) request["params"] = parameters;
            return new McpServer(client, new JsonCodec()).HandleAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static IDictionary<string, object> CallTool(string name, IDictionary<string, object> arguments, IPromptClient client) {
            return Call("tools/call", new Dictionary<string, object> {
                { "name", name }, { "arguments", arguments }
            }, client);
        }
    }

    internal static class McpAssertions {
        public static IDictionary<string, object> Object(object value) {
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null) throw new InvalidDataException("Expected an object.");
            return result;
        }

        public static object[] Array(object value) {
            object[] result = value as object[];
            if (result == null) throw new InvalidDataException("Expected an array.");
            return result;
        }

        public static IDictionary<string, object> Result(IDictionary<string, object> envelope) {
            return Object(envelope["result"]);
        }

        public static IDictionary<string, object> ToolResult(IDictionary<string, object> envelope) {
            return Result(envelope);
        }

        public static IDictionary<string, object> ToolPayload(IDictionary<string, object> envelope) {
            return Object(ToolResult(envelope)["structuredContent"]);
        }

        public static bool HasTool(IDictionary<string, object> envelope, string name) {
            foreach (object value in Array(Result(envelope)["tools"])) {
                if (System.Object.Equals(Object(value)["name"], name)) return true;
            }
            return false;
        }

        public static string ToolSchemaJson(IDictionary<string, object> envelope, string name) {
            foreach (object value in Array(Result(envelope)["tools"])) {
                IDictionary<string, object> tool = Object(value);
                if (System.Object.Equals(tool["name"], name)) return new JsonCodec().Serialize(tool["inputSchema"]);
            }
            throw new InvalidDataException("Tool not found: " + name);
        }
    }
}
