---
name: cue-prompts
description: Route every user-facing question or response request—including clarification, approval, missing information, open text, single choice, and multiple choice—through codex_cue ask_options.
---

# Cue Prompts

1. Call `codex_cue.ask_options` instead of asking in prose. Send prompt text only in MCP JSON; never use PowerShell, shell, clipboard, or files.
2. Use stable IDs. Let the task determine question and option counts. Default choices to `single`, `required: true`, `allowOther: true`; use `multiple` only for independent selections.
3. For open text use `options: []`, `mode: single`, `required: true`, `allowOther: true`, plus `otherLabel`.
4. Put a justified recommendation first and mark it `recommended: true`. Use `autoResolutionMs` only when a non-blocking question has valid recommendations.
5. `submitted` is an answer; `skipped` is an explicit skip; `cancelled` means the user cancelled or closed the window; `timed_out` means no action. Never invent an answer for the last three.
6. If the MCP tool fails, ask one concise plain-text question. Do nothing when no response is needed.
