# TASK-CFO-P8-011 — Frontend Container and Full Local Deployment

## Objective

Containerize the React frontend last and provide one-command local deployment.

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

P8-010 complete.

## Scope

Add multi-stage frontend image, static runtime server, same-origin API reverse proxy if suitable, Compose integration, health, and Playwright.

## Out of scope

Frontend redesign, auth, streaming, SSR migration, Ollama container.

## Implementation requirements

Preserve existing UI. Prefer Nginx same-origin proxy to API. Publish frontend for users; API publication may remain diagnostic/configurable. Keep MCP/PostgreSQL internal. Preserve Ollama access only from API. Verify unit tests, build, and E2E against containers.

## Acceptance criteria

`docker compose up` starts full app; browser reaches UI/API; five MVP scenarios render; frontend and Playwright pass; MCP/PostgreSQL are not exposed.

## Required validation

Run frontend install/test/build, Compose build/up/ps, Playwright against containers, and full .NET validation.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
