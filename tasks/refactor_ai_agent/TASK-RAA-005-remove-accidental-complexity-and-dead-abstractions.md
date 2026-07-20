# TASK-RAA-005 — Remove Accidental Complexity and Dead Abstractions

## Objective

Remove remaining unnecessary complexity from `CfoAgent.Api` after the architecture boundaries are stable.

## Prerequisite

- TASK-RAA-004 complete

## Scope

Review and simplify:

- interfaces
- factories
- registries
- base classes
- wrappers
- DTO mappings
- helpers
- options classes
- validators
- folders
- unused code
- duplicate tests

## Required review

Look for:

- interfaces with one trivial implementation and no test/replacement value
- factories that only call constructors
- registries with fixed single-purpose mappings
- decorators with no cross-cutting value
- generic base classes used once
- pass-through services
- duplicate DTOs
- duplicate validation
- unnecessary options wrappers
- unused folders/files
- stale transitional code
- dead comments and obsolete docs references

## Rules

- Delete only with evidence.
- Keep architectural ports that protect real external boundaries.
- Keep test seams that materially improve testing.
- Do not flatten the project into tightly coupled code.
- Do not perform style-only rewrites.
- Do not change behavior.
- Do not change other projects.

## Acceptance criteria

- Accidental complexity identified in Task 1 is resolved where approved.
- Necessary architectural boundaries remain.
- Dead code and empty folders are removed.
- Tests remain meaningful.
- Full solution remains green.

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
