# TASK-CFO-P8-008 — Remove Finance Persistence from CfoAgent.Api

## Objective

Remove direct finance persistence and obsolete local Finance fallback from the AI Agent project.

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

P8-007 complete.

## Scope

Delete API finance DbContext/entities used only for persistence, migrations, seeding, DB connection config, direct query services, and local Finance fallback.

## Out of scope

Changing MCP contracts, moving regression forecast unless necessary, ChromaDB changes, Docker.

## Implementation requirements

Retain only transport-neutral finance result DTOs and deterministic forecast logic required by agents. Remove EF providers from API if unused. Update tests to fake Finance MCP rather than temporary finance DBs. Add an architectural assertion that API has no finance persistence/provider dependency.

## Acceptance criteria

API has no finance DbContext, SQLite/Npgsql finance provider, migrations, seed commands, or finance connection string. All finance data comes through Finance MCP. Forecast remains deterministic.

## Required validation

Run focused tests required by this task, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Also inspect `dotnet list src/CfoAgent.Api/CfoAgent.Api.csproj package`.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
