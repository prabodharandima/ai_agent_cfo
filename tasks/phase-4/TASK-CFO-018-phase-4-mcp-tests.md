# TASK-CFO-018 — Phase 4 MCP test gate

## Phase

Phase 4 — MCP integrations

## Goal

Validate MCP discovery, invocation, input restrictions, and fallback behavior.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-017` completed.

## Implementation steps


1. Test Finance MCP tool discovery.
2. Invoke each finance tool with valid data.
3. Test invalid dates, excessive date range, missing database, and cancellation.
4. Test filesystem allow-list and path traversal rejection.
5. Test monolith fallback when Finance MCP is stopped.
6. Test that MCP and local calculations agree for selected fixtures.
7. Add `docs/PHASE-4-RESULTS.md`.


## Expected files or areas


- MCP integration tests
- security/path tests
- `docs/PHASE-4-RESULTS.md`


## Acceptance criteria


- Required MCP tests pass.
- No tool can write finance data.
- Path traversal is rejected.
- Fallback behavior is visible and deterministic.


## Validation commands

```bash
dotnet test CfoAgent.sln --configuration Release
```

## Constraints and non-goals

- Do not continue to API/UI if MCP integration is not stable.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-018 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
