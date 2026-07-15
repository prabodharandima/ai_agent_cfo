# TASK-CFO-003 — Create finance data model and SQLite persistence

## Phase

Phase 1 — Structured finance data

## Goal

Create the smallest structured data model required by the five MVP questions.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 0 completed.

## Implementation steps


1. Add EF Core SQLite packages compatible with .NET 10.
2. Create entities for `Sale`, `Product`, and `BudgetTarget`.
3. Keep fields limited to dates, quantity, prices, discounts, cost, region, category, and budget assumptions needed by the MVP.
4. Create `FinanceDbContext`.
5. Configure decimal precision and useful indexes.
6. Add the first migration.
7. Register the DbContext in the monolith.
8. Add a development-only database initialization service that applies migrations.
9. Do not add repository or unit-of-work wrappers; query EF Core directly through focused services.


## Expected files or areas


- `src/CfoAgent.Api/Data/`
- `src/CfoAgent.Api/Models/Finance/`
- EF Core migration files


## Acceptance criteria


- Migration creates the required tables and indexes.
- API starts and creates/updates the configured SQLite database.
- Entities are not exposed directly from controllers.
- No generic repository is present.


## Validation commands

```bash
dotnet ef database update --project src/CfoAgent.Api --startup-project src/CfoAgent.Api
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not seed data or implement calculations yet.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-003 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
