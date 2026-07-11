---
name: next-step-options
description: Use whenever Codex has genuinely completed the user's requested task and is about to finish the turn. Generate 2-3 relevant next-task choices and present them through the codex_option_prompts ask_options MCP tool instead of listing suggestions only in assistant prose.
---

# Next Step Options

1. Confirm the current requested task is genuinely complete: all required work and proportionate verification are finished, and the turn is not blocked or waiting for required user input.
2. Derive exactly 2 or 3 concrete next tasks from the outcome. Prefer actions that extend, validate, publish, document, or improve the completed work. Avoid generic filler and duplicate choices.
3. Put the strongest recommendation first when one is justified. Mark it `recommended: true` and give each option a short description of its effect.
4. Call `ask_options` from `codex_option_prompts` with one single-select question such as `What would you like to do next?`, localized to the user's language. Set `allowOther: true` so the user can enter another task. Send all text directly in the MCP JSON arguments; never transport it through PowerShell, another shell, a clipboard, or a temporary file.
5. Do not also print the same 2-3 choices as a prose or bullet list before the tool call.
6. Treat a submitted choice or custom answer as the user's next request and continue with it immediately when it is safely in scope. If the result is `cancelled` or `timed_out`, finish the completed task without inventing a selection.
7. Repeat this workflow after completing a selected follow-up task. Do not invoke it for greetings, status-only replies, blockers, incomplete work, or when the user explicitly asks for no follow-up suggestions.
8. If `ask_options` is unavailable or returns an MCP error, include 2-3 concise next tasks in the final response as a fallback.
