# TASK-MAF-008 — Full Regression and Final Documentation

## Goal

Validate the integrated Microsoft Agent Framework changes and align documentation with verified code.

## Before validation

Read repository instructions, the integration README, all earlier result files, Git history, and current code.

## Run

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
docker compose config
git diff --check
```

Run available integration and smoke tests for:

- sales summary;
- sales comparison;
- top products;
- forecast;
- knowledge;
- mixed request;
- prompt-injection attempt;
- streaming;
- session follow-up;
- cancellation;
- dependency failures.

## Documentation

Update only verified current-state behavior in:

- `APPLICATION_ARCHITECTURE.md`
- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- README/setup documentation

Document:

- framework packages and versions;
- middleware;
- structured output;
- streaming endpoint;
- sessions;
- RAG context provider;
- telemetry;
- security boundaries;
- deterministic boundaries;
- remaining limitations.

Remove or correct future-state notes that are now implemented or no longer planned.

## Result file

Create `TASK-MAF-008-RESULT.md` containing:

- files changed;
- final package versions;
- Debug and Release results;
- test totals;
- integration/smoke results;
- warnings;
- known limitations;
- blockers;
- confirmation that deterministic finance calculations, typed MCP routing, validation, authorization, and allow-lists remain unchanged.
