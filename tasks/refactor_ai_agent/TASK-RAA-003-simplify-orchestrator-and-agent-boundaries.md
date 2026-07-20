# TASK-RAA-003 — Simplify Orchestrator and Agent Boundaries

## Objective

Refactor the core application flow so `CfoAgent.Api` clearly follows an Orchestrator–Worker multi-agent pattern without unnecessary coordination layers.

## Prerequisites

- TASK-RAA-001 complete
- TASK-RAA-002 complete
- Target architecture and plan approved by repository state

## Scope

Refactor only:

- API request entry point
- CFO orchestrator
- specialist agents
- result composition
- related tests

## Required behavior

- HTTP endpoint remains thin.
- CFO orchestrator owns coordination only.
- Specialist agents own focused business capabilities.
- Orchestrator does not directly perform MCP, vector DB, or infrastructure calls.
- Specialist agents do not own HTTP concerns.
- Mixed requests may invoke multiple workers and compose results.
- Deterministic calculations remain deterministic.
- Cancellation flows end-to-end.
- Existing public response contract remains unchanged.

## Simplification rules

- Remove duplicate orchestration layers.
- Merge trivial pass-through classes where justified.
- Keep interfaces only when they protect a real boundary or test seam.
- Do not introduce new patterns beyond those approved in Task 2.
- Do not refactor MCP, vector-store, or LLM implementation details yet except minimal changes required for compilation.
- Do not start Task 4.

## Tests required

Prove:

- each intent routes correctly
- mixed requests invoke the expected workers
- unsupported intent behavior remains
- worker failure propagates correctly
- cancellation propagates
- result composition remains stable
- public API behavior remains unchanged

## Acceptance criteria

- One clear orchestrator exists.
- Worker responsibilities are focused.
- Duplicate coordination code is removed.
- Public behavior is preserved.
- Full tests pass.

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
git diff --check
```

Stop after this task.
