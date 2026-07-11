---
name: next-step-options
description: After genuinely completing a task, offer the configured number of relevant next tasks through codex_cue ask_options.
---

# Next Step Options

1. Run only after complete work, never for blockers, greetings, status replies, or when disabled. Use the hook's configured option count; default to 3.
2. Create that many specific, non-duplicate next tasks. Put the best justified recommendation first with `recommended: true`.
3. Call `codex_cue.ask_options` with one localized single-choice question, `allowOther: true`, localized `cancelLabel: Skip`, and `cancelResult: skipped`. Send text only in MCP JSON—never PowerShell, shell, clipboard, or files.
4. Continue a `submitted` choice. On `skipped`, finish immediately. On `cancelled` or `timed_out`, finish without assuming a choice. Never repeat the same completion prompt.
5. Do not duplicate options in prose. If the tool fails, list the configured number of concise choices once.
