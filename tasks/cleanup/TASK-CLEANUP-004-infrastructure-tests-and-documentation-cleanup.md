# TASK-CLEANUP-004 — Infrastructure, Tests, and Documentation Cleanup

## Objective

Remove stale infrastructure files, obsolete test artifacts, duplicate scripts, and misleading documentation.

## Scope

Clean:

- Dockerfiles and Compose references
- scripts
- duplicate/obsolete tests
- temporary/generated tracked artifacts
- stale configuration examples
- obsolete documentation references
- empty folders
- `.gitignore` and `.dockerignore` gaps

## Requirements

- Remove scripts only when no task, README, CI command, or workflow references them.
- Remove duplicate tests only when equivalent or stronger coverage remains.
- Remove obsolete stdio/SQLite statements from active architecture/setup docs.
- Preserve historical phase-result files, but mark superseded architecture where needed.
- Remove tracked generated artifacts such as `bin`, `obj`, `node_modules`, coverage output, traces, screenshots, videos, logs, and temp DB files only when safely reproducible.
- Improve ignore files only for proven generated artifacts.
- Validate Docker Compose before deleting anything.
- Do not delete required migrations or seed assets.
- Update the cleanup inventory.

## Acceptance criteria

- No stale active documentation describes removed architecture.
- No required script/test helper is removed.
- Generated artifacts are not tracked.
- Docker Compose remains valid.
- Backend and frontend validation pass.

## Validation

```bash
git ls-files
git status
docker compose config
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
npm --prefix src/cfo-agent-ui ci
npm --prefix src/cfo-agent-ui test
npm --prefix src/cfo-agent-ui run build
git diff --check
```

Stop after this task.
