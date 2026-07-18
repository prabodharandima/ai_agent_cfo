# TASK-CFO-P7-007 — Opt-in Live Ollama Tests

## Objective

Add opt-in tests for a real local Ollama instance using `llama3.2:3b` without affecting the normal offline suite.

## Mandatory reading

Read completely:

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- `EXECUTION-ORDER.md`
- `tasks/phase-7/PHASE-7-EXECUTION-ORDER.md`
- this task file

Inspect the actual repository before making changes.

## Scope

Opt-in test category/trait and optional script; short live completion and application smoke flow.

## Out of scope

CI dependence, automatic installation/download, load tests, replacement of fake tests.

## Requirements

Use an explicit opt-in variable such as `CFO_AGENT_RUN_OLLAMA_TESTS=true`, adapting to repository conventions. Read endpoint/model from configuration. Verify endpoint reachability, model availability, one basic `IChatClient` completion, one sales-summary flow, provider/model metadata, and—when local Chroma test data is available—one grounded knowledge flow. Keep prompts short, responses bounded, timeouts generous but finite, and tests sequential to reduce memory pressure. Never download the model automatically. Normal `dotnet test` must not call Ollama.

## Acceptance criteria

- Default tests do not call Ollama.
- Opt-in mode validates `llama3.2:3b`.
- Missing endpoint/model gives a clear actionable skip or failure.
- Tests are sequential and bounded.
- No automatic model download occurs.

## Validation

Run the normal offline validation first. If Ollama is available, run the documented opt-in command and record model, elapsed time, and pass/skip/fail counts. Lack of Ollama in the execution environment is not a blocker if the opt-in implementation and instructions are correct.

## Completion report

Report files created/modified, design decisions, commands, focused tests, total test count, build result, assumptions, deviations, blockers, and whether every acceptance criterion is complete.

Stop after this task. Do not start the next task.

