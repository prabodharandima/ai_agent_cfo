# TASK-CFO-P8-007 — API HTTP MCP Clients and Failure Policy

## Objective

Replace API child-process MCP clients with remote HTTP MCP clients and enforce no Finance fallback.

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

P8-005 and P8-006 complete.

## Scope

Endpoint-based clients, lazy init, capability discovery, timeout/cancellation, readiness, and error mapping.

## Out of scope

Final removal of finance persistence, Docker Compose, frontend, auth.

## Implementation requirements

Replace ServerProjectPath with BaseUrl config. Remove process launching/disposal. Finance unavailable/timeout/capability mismatch must produce sanitized dependency failure/503; caller cancellation remains distinct. Knowledge fallback may remain explicit development-only and must be disableable. Startup/DI must not call network. Preserve fixed agent-to-tool mapping and add fake/test-server coverage.

## Acceptance criteria

API launches no child processes; Finance works over HTTP; failures return sanitized 503 with no local finance fallback; Knowledge fallback is explicit; success contracts remain compatible.

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
