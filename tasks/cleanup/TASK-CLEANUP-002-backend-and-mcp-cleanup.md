# TASK-CLEANUP-002 — Backend and MCP Cleanup

## Objective

Remove only proven unused .NET backend and MCP code, packages, configuration, and folders.

## Mandatory reading

Read all core architecture/instruction files plus:

- `docs/CLEANUP-INVENTORY.md`
- `tasks/cleanup/README-CLEANUP.md`
- `tasks/cleanup/CLEANUP-EXECUTION-ORDER.md`
- this task file

## Scope

Clean:

- `src/CfoAgent.Api`
- Finance MCP
- Knowledge File MCP
- shared .NET projects if any
- package/project references
- backend configuration/options
- obsolete backend tests/helpers tied only to removed code

## Requirements

- Remove only items classified as safe.
- Re-verify every candidate before deletion.
- Remove stale stdio/process-launch code if unused.
- Remove stale SQLite code/packages/configuration if unused.
- Remove obsolete Finance fallback code if Phase 8 removed it.
- Remove duplicate DTOs only when one canonical contract remains.
- Remove unused config only after checking appsettings, environment variables, Compose, tests, scripts, options validation, and docs.
- Preserve MCP contracts, EF migrations, health checks, CLI commands, DI-only types, serialization DTOs, and reflection-discovered MCP tools.
- Remove empty folders after safe deletion.
- Update `docs/CLEANUP-INVENTORY.md` with outcomes.

## Acceptance criteria

- No approved dead backend/MCP code remains.
- No DI, MCP, EF, configuration, or serialization behavior is broken.
- No public contract changes.
- Full Debug and Release tests pass.

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
dotnet list CfoAgent.sln package
git diff --check
```

Stop after this task.
