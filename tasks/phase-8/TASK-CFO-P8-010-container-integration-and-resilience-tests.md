# TASK-CFO-P8-010 — Container Integration and Resilience Gate

## Objective

Prove the separated backend with real container tests and failure scenarios.

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

P8-009 complete.

## Scope

Create isolated Compose/test profile and Windows-friendly script covering PostgreSQL, both MCPs, API, Chroma, and outages.

## Out of scope

Frontend container, load testing, auth, Ollama download.

## Implementation requirements

Disable local fallback. Verify all five MVP prompts with Mock. Stop Finance MCP and verify sanitized 503/no DB fallback. Verify Knowledge outage, MCP discovery/tools, PostgreSQL migration/seed, Chroma citations, caller cancellation distinction, internal-only ports, read-only knowledge mount, and absence of API PostgreSQL config. Keep normal unit tests Docker-independent.

## Acceptance criteria

All five scenarios pass through containers; Finance outage returns 503 and no fallback; Knowledge security passes; normal offline tests stay green; repeatable PowerShell command exists.

## Required validation

Run focused tests required by this task, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Run the new isolated container gate and record exact results.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
