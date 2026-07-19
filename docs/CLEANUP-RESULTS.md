# Cleanup Results

## Status

The repository cleanup is complete. Tasks `TASK-CLEANUP-001` through `TASK-CLEANUP-005` were performed in order, and the final Debug, Release, frontend, Docker, container-resilience, and browser gates pass. No feature work or architecture redesign was introduced.

## Removed items

### Files

- `src/CfoAgent.Api/CfoAgent.Api.http`: unused ASP.NET template request for an endpoint that does not exist.
- `tests/CfoAgent.Api.Tests/UnitTest1.cs`: template-named Mock metadata test duplicated by `MockChatClientTests`.
- `src/.gitkeep`, `tests/.gitkeep`, `tools/.gitkeep`, and `data/knowledge/.gitkeep`: redundant anchors in directories containing tracked files.
- `src/cfo-agent-ui/public/icons.svg`: unreferenced Vite scaffold icon sprite.
- `data/imports/.gitkeep`: sole entry in an unused import placeholder.
- `FILE-MANIFEST.md`: incomplete pre-Phase-7 manifest superseded by `git ls-files`.
- `scripts/start-phase-5-e2e-api.ps1`: obsolete SQLite/stdio-era local E2E startup path.
- `src/cfo-agent-ui/playwright.config.ts`: configuration used only by the obsolete local E2E path.

### Folders

- `data/imports/`: empty after its unused anchor was removed.
- `src/cfo-agent-ui/src/assets/`: empty, untracked Vite scaffold directory.

### Code, scripts, configuration, and packages

- Removed one duplicate xUnit test; the final suite contains 175 discovered tests.
- Removed the obsolete frontend `test:e2e` npm script. The supported `test:e2e:container` command remains.
- Removed no runtime classes, public API contracts, MCP tools, deterministic calculations, migrations, seed data, Docker services, configuration keys, NuGet packages, or npm packages.
- Restored committed AI defaults to `Mock` / `DeterministicMock`; Ollama remains available through explicit configuration.
- Updated active runbooks and historical markers to match the Phase 8 PostgreSQL, Streamable HTTP MCP, ChromaDB, and Compose deployment.

## Architecture verification

- `CfoAgent.Api` has no finance `DbContext`, EF migrations, finance seed path, PostgreSQL package, connection string, or local Finance fallback.
- Finance MCP owns `FinanceDbContext`, Npgsql, migrations, deterministic seeding, finance aggregation, and budget lookup.
- Finance MCP and Knowledge File MCP are independently hosted through the official MCP SDK over Streamable HTTP.
- Knowledge File MCP remains read-only; its API local fallback is explicit, secure, Development-only, and disabled in containers.
- ChromaDB remains the semantic RAG and citation store. Raw Knowledge MCP file access does not replace retrieval.
- The React/API contracts, five user workflows, four-agent architecture, Mock/Ollama provider selection, and deterministic finance behavior are unchanged.

## Retained candidates

The following candidates were intentionally retained because safe removal is not proven:

- `coverlet.collector`: no tracked coverage command uses it, but external CI usage is unknown.
- `scripts/run-api.ps1`, `scripts/run-ui.ps1`, `scripts/build.ps1`, and `scripts/test.ps1`: not linked by current docs, but external developer use cannot be excluded.
- Duplicate-test candidates in `MockChatClientGateTests.cs` and `SpecialistAgentTests.cs`: exact equivalent assertion coverage has not been proven.
- SQLite ignore patterns in `.gitignore`: a local ignored legacy database exists, and cleanup must not expose, move, or delete user data.
- `data/cfo-agent.db`: ignored local user data, never tracked or modified by cleanup.
- Historical phase results, migration designs, and completed task files: retained for traceability and distinguished from current runbooks.
- EF Core migration designer/source files, MCP tool classes, DI-discovered health and endpoint types, test fixtures/attributes, transport DTOs, and the approved Knowledge local fallback: all have reflection, tooling, runtime, test, or compatibility use.
- `.agents/`: repository metadata directory; it is not an application empty-folder candidate.

No newly discovered risky candidate was removed during the final gate.

## Final validation

| Gate | Result |
|---|---|
| Restore | Passed for all four .NET projects. |
| Debug build | Passed with 0 warnings and 0 errors. |
| Debug tests | 175 total: 167 passed, 0 failed, 8 opt-in tests skipped. |
| Release tests | 175 total: 167 passed, 0 failed, 8 opt-in tests skipped. |
| Frontend install | 120 packages installed; 0 vulnerabilities. |
| Frontend unit tests | 2 files, 10 tests passed. |
| Frontend lint | Passed. |
| Frontend production build | Passed. |
| Compose configuration | Passed. |
| Compose image build | Passed for API, frontend, both MCP services, and one-shot images. |
| Isolated container gate | 3 tests passed; deterministic seed, security boundaries, dependency failures, cancellation, and recovery passed. |
| Playwright container E2E | 7 tests passed against an isolated Mock deployment. |
| Tracked generated-artifact scan | No tracked build, dependency, test-result, coverage, database, log, or binary-log artifact found. |
| Whitespace check | `git diff --check` passed. |

The normal backend suite skips four opt-in live/container categories represented by eight tests. The three Phase 8 container tests were then executed separately by the isolated container gate and passed. Live Ollama tests remain intentionally opt-in and were not required for this offline cleanup gate.

The first sandboxed Debug test attempt could not access Docker Desktop and the first sandboxed Vitest attempt could not spawn Vite's helper process. Both commands passed unchanged when rerun with the required host access. These were environment restrictions, not repository failures.

## Commands

```powershell
git status --short
git log -6 --oneline
git ls-files
dotnet sln CfoAgent.sln list
dotnet list <project> package --no-restore
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
npm --prefix src/cfo-agent-ui ci
npm --prefix src/cfo-agent-ui test -- --run
npm --prefix src/cfo-agent-ui run lint
npm --prefix src/cfo-agent-ui run build
docker version
docker compose config --quiet
docker compose build
./scripts/test-phase-8-containers.ps1 -ProjectName cfo-cleanup-005 -ApiPort 5262 -TimeoutSeconds 240
npm --prefix src/cfo-agent-ui run test:e2e:container
git diff --check
git status
```

Playwright used an isolated Compose project on ports `5174` and `5263` with the integration overlay's deterministic Mock provider. The isolated test containers, networks, and volumes were removed afterward. Existing user Compose resources and persistent volumes were not removed.

## Final assessment

Every `TASK-CLEANUP-005` acceptance criterion is complete. There are no cleanup deviations or blockers.
