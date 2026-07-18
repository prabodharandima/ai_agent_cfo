# TASK-CFO-P7-005 — Resilience, Health, and Logging

## Objective

Add safe operational behavior for the optional local Ollama dependency.

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

Readiness, timeout, cancellation, Problem Details, safe structured logging.

## Out of scope

Automatic model download, background loading, infinite retries, heavy resilience frameworks, monitoring platforms.

## Requirements

Liveness must not depend on Ollama. Readiness checks Ollama only when selected and uses a finite timeout without sending a full CFO prompt. Do not probe Ollama for Mock mode. Map unavailable, timeout, and malformed-response failures to sanitized errors. Caller cancellation must propagate and must not become fallback. Do not silently fall back to Mock unless an explicit existing policy/configuration requires it; prefer a controlled provider error. Log provider, model, operation, duration, outcome, and stable failure category only. Do not log prompts, RAG context, model bodies, sensitive URLs, or stack traces in controlled failures. Startup must remain network-free.

## Acceptance criteria

- Liveness works with Ollama stopped.
- Mock readiness never calls Ollama.
- Ollama readiness handles available/unavailable fake responses.
- API errors are sanitized.
- Caller cancellation propagates.
- Logging contains safe metadata only.

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

