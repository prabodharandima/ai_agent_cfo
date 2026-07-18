# TASK-CFO-P7-002 — Ollama Options and Registration

## Objective

Add strongly typed Ollama configuration and provider selection without making network calls during startup.

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

Configuration models, startup validation, DI/provider selection, development configuration example for `llama3.2:3b`.

## Out of scope

Ollama HTTP requests, agent changes, health checks, live tests, embedding changes.

## Requirements

Extend the existing AI options rather than creating a parallel hierarchy. Support `Mock` and `Ollama`; keep `Mock` as the default. Add configuration equivalent to provider, model, base URL, timeout, temperature, and context length, adapting names to existing conventions. Validate provider, absolute HTTP/HTTPS URL, non-empty model, positive finite timeout, safe context range, and valid temperature. Register exactly one application `IChatClient` for the selected provider. Do not probe or launch Ollama during registration. Add `llama3.2:3b` only through configuration.

## Acceptance criteria

- Mock remains default and existing Mock tests pass.
- Valid Ollama configuration passes.
- Invalid provider/URL/model/timeout/context values fail predictably.
- Startup remains network-free.
- Exactly one selected `IChatClient` is registered.

## Validation

Run focused tests, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

## Completion report

Report files created/modified, design decisions, commands, focused tests, total test count, build result, assumptions, deviations, blockers, and whether every acceptance criterion is complete.

Stop after this task. Do not start the next task.

