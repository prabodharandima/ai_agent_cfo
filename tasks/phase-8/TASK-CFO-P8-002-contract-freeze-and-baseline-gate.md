# TASK-CFO-P8-002 — Contract Freeze and Baseline Gate

## Objective

Freeze chat/API/MCP contracts and establish the pre-refactor regression baseline.

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

P8-001 complete.

## Scope

Add or strengthen tests for API and MCP contracts; create `docs/PHASE-8-BASELINE.md`.

## Out of scope

Transport migration, PostgreSQL implementation, Docker.

## Implementation requirements

Lock five Finance MCP tool names/inputs/outputs/errors, two Knowledge MCP tool contracts/security, five MVP chat scenarios, deterministic finance/forecast values, RAG citations, provider metadata, and sanitized dependency-error shape. Prefer transport-neutral tests.

## Acceptance criteria

Exact baseline counts recorded; Debug and Release pass; no tool or response type renamed; deterministic outputs and citations protected.

## Required validation

Run focused tests required by this task, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Also run Release tests.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
