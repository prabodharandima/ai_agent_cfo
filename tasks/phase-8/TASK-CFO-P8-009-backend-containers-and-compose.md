# TASK-CFO-P8-009 — Backend Containers and Docker Compose

## Objective

Containerize API, both MCP services, PostgreSQL, and ChromaDB.

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

P8-008 complete.

## Scope

Add multi-stage Dockerfiles, `.dockerignore`, internal network, volumes, health checks, environment config, and backend Compose topology.

## Out of scope

Frontend container, Ollama container, auth, cloud deployment.

## Implementation requirements

Publish API only; keep MCP/PostgreSQL internal. PostgreSQL persistent volume and health check; only Finance MCP receives DB connection string. Mount knowledge read-only. Configure API with internal MCP/Chroma URLs and host Ollama URL `http://host.docker.internal:11434`; add host-gateway mapping where needed. Add safe migration/seed startup. Disable Knowledge fallback in container mode.

## Acceptance criteria

All backend services build/start healthy; API has no DB config; MCPs are internal; Finance reaches PostgreSQL; Knowledge reads read-only volume; API reaches both MCPs/Chroma and host Ollama when selected.

## Required validation

Run:

```bash
docker compose config
docker compose build
docker compose up -d
docker compose ps
docker compose logs --no-color
```

Run a backend smoke request and the standard .NET validation. Do not remove persistent user volumes.

## Completion report

Report files created, modified, moved, and deleted; packages and configuration changes; Docker changes; commands; focused and total test results; assumptions; deviations; blockers; and whether every acceptance criterion is complete.

Stop after this task. Do not start the next Phase 8 task.
