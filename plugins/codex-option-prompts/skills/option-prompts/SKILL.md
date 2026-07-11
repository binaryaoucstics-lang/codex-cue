---
name: option-prompts
description: Use whenever Codex is about to ask the user any question or needs any user response, including clarification, confirmation, approval, missing information, preferences, open-ended input, single-select choices, or multi-select choices. Route every such interaction directly through the codex_option_prompts ask_options MCP tool instead of asking in assistant prose.
---

# Option Prompts

1. Call the `ask_options` MCP tool from `codex_option_prompts` immediately before every user-facing question. Do not first ask or list choices in assistant prose.
2. Put all question, description, option, and free-text-label content directly in the MCP JSON arguments. Never use PowerShell, another shell, a clipboard, or a temporary file to transport this text.
3. Send `questions` with stable unique question and option IDs. Keep question and option counts driven by the task; do not pad or truncate them.
4. For an open-ended question, send `options: []`, `mode: single`, `required: true`, `allowOther: true`, and a useful `otherLabel`. Do not invent artificial choices.
5. For choices, default to `mode: single`, `required: true`, and `allowOther: true`. Use `multiple` only when independent selections may coexist.
6. Put a recommended option first when justified, mark it `recommended: true`, and explain its impact without selecting it for the user.
7. Include `autoResolutionMs` only for a non-blocking question whose required answers all have valid recommendations.
8. Treat `submitted` answers as the user's response. Treat `cancelled` and `timed_out` as no response unless the original call explicitly allowed best judgment.
9. If `ask_options` is unavailable or returns an MCP error, ask one concise plain-text question and wait for the user.
10. Do not call the tool when no user response is needed or when existing instructions already resolve the issue safely.
