# Final Validation

TASK-CFO-023 completed this clean-state regression on 2026-07-16. This record documents the reproducible commands that a reviewer can run from the repository root.

## Clean local state

```powershell
docker compose down -v
docker compose up -d
```

`down -v` removes the local ChromaDB Docker volume only. It does not remove source, task, documentation, SQLite seed definitions, or Markdown knowledge files. The seed and RAG ingestion commands recreate the demo state.

## Required validation commands

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --configuration Release --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1

Set-Location src/cfo-agent-ui
npm ci
npm run build
npm test -- --run
npm run test:e2e
```

## Serialized solution regression

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

## TASK-CFO-023 Results

All commands completed successfully after resetting and recreating the local ChromaDB volume.

- `docker compose down -v` and `docker compose up -d`: passed; the ChromaDB container and fresh volume were recreated.
- `dotnet restore CfoAgent.sln`: passed.
- `dotnet build CfoAgent.sln --configuration Release --maxcpucount:1`: passed with 0 warnings and 0 errors.
- `dotnet test CfoAgent.sln --configuration Release --maxcpucount:1`: 118 passed, 0 failed, 0 skipped.
- `npm ci`: passed with 0 reported vulnerabilities.
- `npm run build`: passed.
- `npm test -- --run`: 10 passed, 0 failed across 2 test files.
- `npm run test:e2e`: 7 passed, 0 failed in Chromium. The existing browser harness seeded SQLite, ingested the Markdown knowledge documents into the fresh ChromaDB collection, and exercised the optional MCP configuration.
- `dotnet build CfoAgent.sln --no-restore --maxcpucount:1`: passed with 0 warnings and 0 errors.
- `dotnet test CfoAgent.sln --no-build --maxcpucount:1`: 118 passed, 0 failed, 0 skipped.

The focused replacement test for the old empty test placeholder also passed: 1 passed, 0 failed.

The Playwright process emitted Node's existing `NO_COLOR`/`FORCE_COLOR` environment warning. It did not affect test execution or results.
