# TASK-CLEANUP-005 — Final Regression and Cleanup Gate

## Objective

Perform the final repository-wide verification and document the cleanup outcome.

## Scope

Create:

- `docs/CLEANUP-RESULTS.md`

Perform final dependency, architecture, test, frontend, Docker, and repository hygiene validation.

## Requirements

Re-scan for:

- unused projects/references,
- unused packages,
- empty folders,
- stale configuration,
- old SQLite/stdio/process-launch references,
- tracked generated artifacts,
- obsolete frontend files,
- duplicate tests,
- documentation mismatches.

Do not make broad new deletions. Newly found risky candidates must be documented for later review.

Verify architecture boundaries:

- API has no finance DB ownership,
- Finance MCP owns PostgreSQL,
- MCP services are network-hosted,
- ChromaDB remains RAG,
- frontend/API behavior is unchanged.

Record exact:

- files/folders deleted,
- packages removed,
- code removed,
- tests retained,
- intentionally retained candidates and reasons,
- final test counts,
- Docker validation,
- warnings/errors,
- blockers.

Update `docs/CLEANUP-INVENTORY.md` statuses.

## Acceptance criteria

- Full Debug/Release .NET validation passes.
- Frontend unit/build validation passes.
- Docker Compose validation passes.
- E2E/container validation passes when available.
- No tracked generated artifacts remain.
- `docs/CLEANUP-RESULTS.md` states complete or incomplete.
- No feature work was introduced.

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
npm --prefix src/cfo-agent-ui ci
npm --prefix src/cfo-agent-ui test
npm --prefix src/cfo-agent-ui run build
docker compose config
docker compose build
git diff --check
git status
```

Run documented Playwright and container integration commands when available.

Stop after this task.
