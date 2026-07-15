# TASK-CFO-004 — Seed demo sales data and knowledge documents

## Phase

Phase 1 — Structured finance data

## Goal

Create deterministic demo data sufficient for weekly summaries, comparisons, product rankings, five-year forecasts, and RAG questions.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-003` completed.

## Implementation steps


1. Create an idempotent development seeder.
2. Seed at least five full years of monthly/daily sales records ending around a configurable fixed demo date.
3. Include multiple products, categories, regions, quantities, unit prices, discounts, and costs.
4. Ensure the data contains visible trend, seasonality, and week-to-week differences.
5. Seed annual and monthly budget targets.
6. Create concise Markdown documents in `data/knowledge`:
   - annual sales report,
   - current budget and target,
   - forecast assumptions,
   - market risks,
   - product strategy.
7. Give each document source metadata in front matter or a companion manifest.
8. Add a non-destructive reset/seed development command.


## Expected files or areas


- seeding code under `src/CfoAgent.Api/Data/Seed/`
- `data/knowledge/*.md`
- optional `data/knowledge/manifest.json`


## Acceptance criteria


- Re-running seeding does not duplicate data.
- Data supports every MVP prompt.
- Knowledge files contain facts that can be cited later.
- Seed results are deterministic.


## Validation commands

```bash
dotnet run --project src/CfoAgent.Api -- --seed
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not ingest into ChromaDB yet.
- Do not use random data without a fixed seed.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-004 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
