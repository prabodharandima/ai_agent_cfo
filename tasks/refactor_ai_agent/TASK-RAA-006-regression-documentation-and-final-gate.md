# TASK-RAA-006 — Regression, Documentation, and Final Gate

## Objective

Verify the final `CfoAgent.Api` architecture, update active documentation, and prove behavior remains intact.

## Prerequisite

- TASK-RAA-005 complete

## Scope

Update relevant sections only:

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- active README/setup documentation

Create:

- `docs/CFO-AGENT-API-REFACTOR-RESULTS.md`

## Required verification

Confirm:

- thin HTTP endpoint
- one clear CFO orchestrator
- focused specialist workers
- clean dependency direction
- LLM behind a port
- MCP behind a port/adapter
- vector database behind a port/adapter
- no direct PostgreSQL access
- no infrastructure leakage into agents
- public API unchanged
- cancellation preserved
- sanitized dependency failures preserved
- deterministic finance logic preserved
- no unnecessary framework or pattern added

## Over-engineering gate

Explicitly inspect for remaining:

- unnecessary factories
- registries
- mediator/pipeline layers
- wrapper chains
- duplicate DTOs
- trivial interfaces
- unused abstractions
- custom frameworks
- excessive folders

Document anything intentionally retained and why.

## Acceptance criteria

- Active docs match source.
- Debug and Release tests pass.
- Container/integration tests pass where available.
- No public behavior regression.
- Final design is clearly Orchestrator–Worker with Clean/Hexagonal boundaries.
- Final result document states complete or incomplete.
- No blockers remain.

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
docker compose config
git diff --check
git status
```

Run existing smoke and container integration commands where available.

Stop after this task.
