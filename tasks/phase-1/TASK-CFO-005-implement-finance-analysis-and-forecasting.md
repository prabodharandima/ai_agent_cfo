# TASK-CFO-005 — Implement deterministic finance analysis and forecasting

## Phase

Phase 1 — Structured finance data

## Goal

Implement reliable C#/SQL calculations that agents will call.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-004` completed.

## Implementation steps


1. Inject `TimeProvider`.
2. Implement a focused `SalesAnalysisService` with:
   - current-week summary,
   - previous-week comparison,
   - current-month top products,
   - historical yearly totals,
   - budget target lookup.
3. Define DTOs containing calculated values, period boundaries, and data availability warnings.
4. Implement a simple `SalesForecastingService`.
5. Use a transparent deterministic method such as linear regression over yearly totals, with conservative/expected/optimistic scenarios.
6. Return method name, historical period, inputs, assumptions, and forecast rows.
7. Handle insufficient data explicitly.
8. Keep calculations out of agents and controllers.


## Expected files or areas


- `src/CfoAgent.Api/Features/Sales/`
- `src/CfoAgent.Api/Features/Forecasting/`
- service registrations


## Acceptance criteria


- Service results are deterministic.
- Week boundaries are based on injected time and documented.
- Percentage changes handle zero denominators safely.
- Forecast returns five years and three scenarios.
- No LLM is involved in calculations.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not create agents, endpoints, RAG, or MCP integration yet.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-005 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
