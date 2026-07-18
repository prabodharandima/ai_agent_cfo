# TASK-CFO-P8-001 — Architecture Discovery and Migration Design

## Objective

Create a repository-grounded migration design for separating the API, two MCP servers, PostgreSQL, ChromaDB, frontend, and host Ollama.

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

Phase 7 is complete and the current baseline is green.

## Scope

Documentation only. Create `docs/PHASE-8-MIGRATION-DESIGN.md`.

## Out of scope

Runtime code, packages, tests, Dockerfiles, Compose changes.

## Implementation requirements

Document current finance entities, DbContext, migrations, seeders, services, forecast path, MCP clients/servers, stdio process launching, health checks, config, tests, frontend API URL, and Ollama. Define target ownership, code-move map, transport-neutral contracts, PostgreSQL ownership, Streamable HTTP endpoints, API failure behavior, Docker network/ports/volumes, and a safe incremental migration. Include current/target Mermaid diagrams. Mark uncertainty as TBA rather than guessing.

## Acceptance criteria

Design is grounded in actual classes and paths; removes DB access from API; removes Finance fallback; preserves deterministic forecast logic and ChromaDB; defines two network-hosted MCP services; changes documentation only.

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
