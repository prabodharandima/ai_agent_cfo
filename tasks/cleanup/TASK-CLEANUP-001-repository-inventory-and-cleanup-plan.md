# TASK-CLEANUP-001 — Repository Inventory and Cleanup Plan

## Objective

Create an evidence-based cleanup inventory without deleting or modifying runtime code.

## Mandatory reading

Read completely:

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- `EXECUTION-ORDER.md`
- `tasks/cleanup/README-CLEANUP.md`
- `tasks/cleanup/CLEANUP-EXECUTION-ORDER.md`
- this task file

## Scope

Create:

- `docs/CLEANUP-INVENTORY.md`

Inspect backend, MCP projects, frontend, tests, Docker, scripts, configuration, and documentation.

## Requirements

Classify candidates into:

1. Safe to remove
2. Probably removable but requires proof
3. Must retain
4. Generated/build artifacts
5. Historical documentation
6. Duplicate or stale configuration
7. Duplicate or obsolete tests
8. Unused packages
9. Empty or obsolete folders
10. Unused public/internal types and methods

For every candidate record:

- path,
- symbol or item,
- evidence,
- references searched,
- DI/reflection/configuration/test risk,
- proposed action,
- validation required.

Explicitly inspect for:

- old stdio MCP process code,
- old SQLite artifacts,
- stale Phase 7/8 transitional code,
- obsolete fallback code,
- duplicate DTOs,
- unused options/configuration keys,
- unused packages,
- dead Docker services/ports,
- obsolete scripts,
- unused frontend components/types/styles,
- unused test helpers,
- accidentally tracked generated files.

Do not rely only on IDE unused indicators.

## Acceptance criteria

- `docs/CLEANUP-INVENTORY.md` exists.
- No runtime or test file is modified.
- Every proposed deletion has evidence.
- Reflection, DI, EF Core, MCP discovery, serialization, configuration, and tests are considered.
- The repository baseline remains green.

## Validation

```bash
git status
git ls-files
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
npm --prefix src/cfo-agent-ui ci
npm --prefix src/cfo-agent-ui test
npm --prefix src/cfo-agent-ui run build
```

Confirm only the inventory document changed.

Stop after this task.
