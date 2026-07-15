# TASK-CFO-006 — Phase 1 core test gate

## Phase

Phase 1 — Structured finance data

## Goal

Create comprehensive tests for data seeding, date handling, sales KPIs, comparisons, ranking, budget lookup, and forecasting.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-005` completed.

## Implementation steps


1. Use a temporary SQLite database for integration tests.
2. Use a fake/fixed `TimeProvider`.
3. Test current-week period boundaries.
4. Test total revenue, order count, average order value, gross profit, and margin.
5. Test week-over-week comparison including zero previous revenue.
6. Test top-five product ranking.
7. Test five-year forecast shape, scenario ordering, and insufficient-data behavior.
8. Test idempotent seeding.
9. Add a short phase-gate report to `docs/PHASE-1-RESULTS.md`.
10. Fix implementation defects found by the tests; do not weaken expected behavior.


## Expected files or areas


- `tests/CfoAgent.Api.Tests/Finance/`
- `docs/PHASE-1-RESULTS.md`


## Acceptance criteria


- All backend tests pass.
- Tests are deterministic and independent of the real current date.
- Phase gate report lists commands and results.


## Validation commands

```bash
dotnet test CfoAgent.sln --configuration Release
```

## Constraints and non-goals

- Do not begin AI or agent work if this gate fails.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-006 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
