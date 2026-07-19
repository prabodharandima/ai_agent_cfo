# TASK-CFO-P8-012 — Documentation, Cleanup, and Phase 8 Gate

## Objective

Update all architecture/setup documentation, remove obsolete artifacts, and complete the Phase 8 gate.

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

P8-011 complete.

## Scope

Update `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, `IMPLEMENTATION-PLAN.md`, `README.md`; create `docs/PHASE-8-RESULTS.md`; remove stale SQLite/stdio/process-launch artifacts.

## Out of scope

New runtime features, auth, OpenAI, Kubernetes/cloud.

## Implementation requirements

Document two Streamable HTTP MCP containers, exclusive PostgreSQL ownership by Finance MCP, no API DB access, no Finance fallback, Knowledge fallback policy, Docker network/ports/volumes/health/startup, and host Ollama. Update Mermaid diagrams. Remove active SQLite and ServerProjectPath references. Preserve historical docs but mark superseded architecture. Add troubleshooting. Record exact Debug/Release/frontend/E2E/container counts and final gate status.

## Acceptance criteria

Docs match code; no active stdio/SQLite claims; no API finance DB ownership; all offline, Release, frontend, E2E, and container gates pass; Phase 8 status explicit.

## Required validation

Run all final .NET, Release, frontend, Compose, container-integration, and Playwright gates and record exact results.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
