# TASK-CFO-P8-003 — Move Finance Ownership to Finance MCP

## Objective

Move finance persistence/query ownership and deterministic finance calculations out of the API path and into Finance MCP.

## Binding Phase 8 decisions

- `CfoAgent.Api` remains the AI Agent/orchestration service.
- Finance MCP and Knowledge File MCP are two separate hosted services and containers.
- PostgreSQL replaces SQLite and is owned only by Finance MCP.
- `CfoAgent.Api` must not connect to PostgreSQL.
- Remove local Finance fallback; Finance MCP failures produce controlled dependency errors, normally HTTP 503.
- Knowledge File MCP development fallback may remain only when explicitly configured and secure.
- Dedicated container integration tests disable local fallback.
- Prefer Streamable HTTP through the official MCP C# SDK.
- ChromaDB remains the RAG vector store.
- Ollama remains on the Windows host and is reached through `host.docker.internal`.
- MCP services remain unauthenticated and internal-network only in Phase 8.
- Frontend containerization occurs after backend integration.

## Mandatory reading

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- `EXECUTION-ORDER.md`
- `tasks/phase-8/README-PHASE-8.md`
- `tasks/phase-8/PHASE-8-EXECUTION-ORDER.md`
- this task file

Inspect the actual repository before editing. Repository code is the source of truth.

## Prerequisites

P8-002 complete.

## Scope

Move/recreate finance entities, EF context/configuration, seed logic, sales aggregation, budget lookup, and historical aggregation under Finance MCP.

## Out of scope

PostgreSQL provider switch, HTTP transport, Docker, frontend.

## Implementation requirements

Preserve frozen results and TimeProvider behavior. Keep forecast regression/scenarios in API using historical totals returned by Finance MCP. Use a minimal shared contract project only if necessary; do not create a generic shared business layer. Keep solution buildable and mark transitional API code for P8-008 removal.

## Acceptance criteria

Finance MCP independently executes all five operations; results match baseline; forecast path remains valid; no unexplained formula duplication; existing API behavior stays green.

## Required validation

Run focused tests required by this task, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
