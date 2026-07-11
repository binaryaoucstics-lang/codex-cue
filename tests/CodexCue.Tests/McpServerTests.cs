using System.Collections.Generic;
using CodexCue.Mcp;

namespace CodexCue.Tests {
    internal static class McpServerTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Mcp initialize exposes stable server contract", delegate {
                IDictionary<string, object> response = McpFixtures.Call("initialize", new Dictionary<string, object>(), new FakePromptClient());
                IDictionary<string, object> result = McpAssertions.Result(response);
                Assert.Equal("2024-11-05", result["protocolVersion"]);
                Assert.Equal("codex-cue", McpAssertions.Object(result["serverInfo"])["name"]);
            });

            tests.Add("Mcp tools list exposes stable tools", delegate {
                IDictionary<string, object> result = McpFixtures.Call("tools/list", null, new FakePromptClient());
                Assert.True(McpAssertions.HasTool(result, "ask_options"));
                Assert.True(McpAssertions.HasTool(result, "option_prompt_status"));
            });

            tests.Add("Mcp ask options schema keeps dynamic counts", delegate {
                IDictionary<string, object> result = McpFixtures.Call("tools/list", null, new FakePromptClient());
                string schema = McpAssertions.ToolSchemaJson(result, "ask_options");
                Assert.True(schema.Contains("questions"));
                Assert.True(schema.Contains("allowOther"));
                Assert.True(schema.Contains("recommendedOptionId"));
                Assert.False(schema.Contains("maxItems"));
            });

            tests.Add("Mcp ask options advertises concise result semantics", delegate {
                IDictionary<string, object> result = McpFixtures.Call("tools/list", null, new FakePromptClient());
                string json = new JsonCodec().Serialize(result);
                Assert.True(json.Contains("native Windows"));
                Assert.True(json.Contains("skipped"));
                Assert.True(json.Contains("cancelled"));
                Assert.True(json.Contains("readOnlyHint"));
                Assert.True(json.Contains("question"));
            });

            tests.Add("Mcp ask options returns structured answers", delegate {
                FakePromptClient prompt = FakePromptClient.Submit("q1", "a");
                IDictionary<string, object> result = McpFixtures.CallTool("ask_options", Fixtures.Object("new-multi-question.json"), prompt);
                Assert.Equal("submitted", McpAssertions.ToolPayload(result)["status"]);
                Assert.Equal(false, McpAssertions.ToolResult(result)["isError"]);
            });

            tests.Add("Mcp validation failure returns invalid params and stable code", delegate {
                IDictionary<string, object> result = McpFixtures.CallTool("ask_options", Fixtures.Object("invalid-duplicate-id.json"), new FakePromptClient());
                IDictionary<string, object> error = McpAssertions.Object(result["error"]);
                Assert.Equal(-32602, error["code"]);
                Assert.Equal("INVALID_REQUEST", McpAssertions.Object(error["data"])["code"]);
            });

            tests.Add("Mcp status never exposes prompt or answer text", delegate {
                FakePromptClient prompt = FakePromptClient.WithStatus(1, 2);
                IDictionary<string, object> result = McpFixtures.CallTool("option_prompt_status", new Dictionary<string, object>(), prompt);
                string json = new JsonCodec().Serialize(result);
                Assert.True(json.Contains("queuedCount"));
                Assert.False(json.Contains("question"));
                Assert.False(json.Contains("answers"));
            });

            tests.Add("Mcp empty list methods and ping are supported", delegate {
                Assert.Equal(0, McpAssertions.Array(McpAssertions.Result(McpFixtures.Call("resources/list", null, new FakePromptClient()))["resources"]).Length);
                Assert.Equal(0, McpAssertions.Array(McpAssertions.Result(McpFixtures.Call("prompts/list", null, new FakePromptClient()))["prompts"]).Length);
                Assert.True(McpAssertions.Result(McpFixtures.Call("ping", null, new FakePromptClient())).Count == 0);
            });
        }
    }
}
