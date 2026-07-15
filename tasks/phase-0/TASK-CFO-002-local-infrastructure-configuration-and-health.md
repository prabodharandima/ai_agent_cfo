# TASK-CFO-002 — Local infrastructure, configuration, and health checks

## Phase

Phase 0 — Repository bootstrap

## Goal

Add ChromaDB local infrastructure and a minimal operational baseline.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-001` completed.

## Implementation steps


1. Add `docker-compose.yml` containing ChromaDB with a persisted local volume and a stable local port.
2. Add strongly typed options for database, Chroma, AI provider, MCP processes/endpoints, and frontend origin.
3. Set AI provider to `Mock`; reject unsupported provider values at startup.
4. Add `/health/live` and `/health/ready`.
5. Readiness must check required local dependencies that already exist; Chroma may report unhealthy until Docker is running, but the application must return a controlled result.
6. Add OpenAPI/Swagger in development.
7. Add a simple root endpoint that identifies the application and mock mode.
8. Document local startup commands in the root README.


## Expected files or areas


- `docker-compose.yml`
- `src/CfoAgent.Api/Configuration/`
- health-check registration and endpoints
- root `README.md`


## Acceptance criteria


- Docker Compose starts ChromaDB.
- Backend starts using development configuration.
- Liveness succeeds.
- Readiness reports Chroma status without crashing.
- Configuration is validated during startup.


## Validation commands

```bash
docker compose config
docker compose up -d
dotnet run --project src/CfoAgent.Api --no-build
```

## Constraints and non-goals

- Do not add a database schema, RAG collections, or MCP servers in this task.

## Additional notes

Stop the manually started API after checking endpoints. Do not leave test processes running.

## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-002 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
