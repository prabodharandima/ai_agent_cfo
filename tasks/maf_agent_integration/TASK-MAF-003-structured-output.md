# TASK-MAF-003 — Structured LLM Output

## Goal

Use Microsoft Agent Framework structured output where it improves reliability.

## Before editing

Read repository instructions, the integration README, all earlier result files, Git history, and the current implementation.

## Target cases

1. Intent classification.
2. Sales-summary date-range interpretation.

## Requirements

- Create typed output models.
- Keep deterministic intent fallback.
- Keep all existing C# date validation and canonicalization.
- Preserve public API behavior.
- Do not use LLM output as authoritative finance data.
- Do not remove existing safety checks.

## Tests

Cover:

- valid structured intent;
- malformed intent fallback;
- valid date output;
- malformed date output;
- future date rejection;
- invalid date ordering;
- maximum range;
- MCP not called after validation failure;
- cancellation.

Run focused tests, build, full tests, and `git diff --check`.

## Documentation

After tests pass, update only relevant current-state architecture sections describing classification and date interpretation.

## Result file

Create `TASK-MAF-003-RESULT.md` with files changed, models added, fallback behavior, tests, docs, blockers, and completion status.
