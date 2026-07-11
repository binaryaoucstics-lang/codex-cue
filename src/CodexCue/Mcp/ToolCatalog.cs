using System.Collections.Generic;

namespace CodexCue.Mcp {
    public static class ToolCatalog {
        public static object[] Tools() {
            return new object[] { AskOptions(), Status() };
        }

        private static IDictionary<string, object> AskOptions() {
            IDictionary<string, object> optionObject = D(
                "type", "object",
                "properties", D(
                    "id", D("type", "string"),
                    "label", D("type", "string"),
                    "description", D("type", "string"),
                    "recommended", D("type", "boolean", "default", false)),
                "required", new object[] { "label" });
            IDictionary<string, object> options = D(
                "type", "array",
                "items", D("oneOf", new object[] { D("type", "string"), optionObject }));
            IDictionary<string, object> question = D(
                "type", "object",
                "properties", D(
                    "id", D("type", "string"),
                    "prompt", D("type", "string"),
                    "question", D("type", "string", "description", "Alias for prompt."),
                    "description", D("type", "string"),
                    "mode", D("type", "string", "enum", new object[] { "single", "multiple" }, "default", "single"),
                    "required", D("type", "boolean", "default", true),
                    "allowOther", D("type", "boolean", "default", true),
                    "otherLabel", D("type", "string"),
                    "options", options),
                "required", new object[] { "id" },
                "anyOf", new object[] {
                    D("required", new object[] { "prompt" }),
                    D("required", new object[] { "question" })
                });

            IDictionary<string, object> schema = D(
                "$schema", "https://json-schema.org/draft/2020-12/schema",
                "type", "object",
                "properties", D(
                    "sessionId", D("type", "string"),
                    "title", D("type", "string"),
                    "questions", D("type", "array", "items", question),
                    "reviewMode", D("type", "string", "enum", new object[] { "auto", "always", "never" }, "default", "auto"),
                    "autoResolutionMs", D("type", "integer", "minimum", 60000, "maximum", 240000),
                    "maxWaitMs", D("type", "integer", "minimum", 1, "default", 900000),
                    "question", D("type", "string"),
                    "header", D("type", "string"),
                    "options", options,
                    "recommendedOptionId", D("type", "string"),
                    "uiMode", D("type", "string", "enum", new object[] { "overlay", "browser", "none" }),
                    "fallbackToBrowser", D("type", "boolean")),
                "anyOf", new object[] {
                    D("required", new object[] { "questions" }),
                    D("required", new object[] { "question" })
                });

            return D(
                "name", "ask_options",
                "description", "Ask the user any question in the native Windows wizard. Use for every clarification, confirmation, approval, missing detail, preference, open-ended response, single-select choice, or multi-select choice instead of asking in assistant prose. Send text directly as MCP JSON; never use PowerShell, shell commands, or files to transport it. For open-ended input, use an empty options array with allowOther true.",
                "inputSchema", schema,
                "annotations", D(
                    "title", "Ask the user",
                    "readOnlyHint", true,
                    "destructiveHint", false,
                    "idempotentHint", false,
                    "openWorldHint", false));
        }

        private static IDictionary<string, object> Status() {
            return D(
                "name", "option_prompt_status",
                "description", "Report local option prompt host availability and queue counts without prompt or answer text.",
                "inputSchema", D(
                    "$schema", "https://json-schema.org/draft/2020-12/schema",
                    "type", "object",
                    "properties", D(),
                    "additionalProperties", false));
        }

        internal static IDictionary<string, object> D(params object[] pairs) {
            Dictionary<string, object> result = new Dictionary<string, object>();
            for (int index = 0; index < pairs.Length; index += 2) {
                result.Add((string)pairs[index], pairs[index + 1]);
            }
            return result;
        }
    }
}
