# TASK-CFO-P8-005 — Finance MCP Streamable HTTP Host

## Objective

Convert Finance MCP from stdio console child process to an independently hosted ASP.NET Core MCP service.

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

P8-004 complete.

## Scope

Use official MCP C# SDK network transport, preferably Streamable HTTP; add liveness/readiness and HTTP MCP tests.

## Out of scope

API client migration, Knowledge MCP, Docker Compose, auth, public host ports.

## Implementation requirements

Expose exactly five approved tools and operational health endpoints. Readiness verifies PostgreSQL/schema. Preserve validation, limits, cancellation, typed errors, and read-only behavior. Configure Kestrel by environment. Remove stdio-only hosting after HTTP parity is proven.

## Acceptance criteria

Independent host starts; HTTP discovery returns exactly five tools; all tools execute over HTTP/PostgreSQL; health works; no stdio hosting dependency remains.

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
