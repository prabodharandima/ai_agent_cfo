# TASK-CFO-009 — Implement Sales and Forecasting agents

## Phase

Phase 2 — Mock LLM and specialist agents

## Goal

Create two specialist agents that use deterministic services and the mock model only for explanation.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-008` completed.

## Implementation steps


1. Implement `SalesAnalysisAgent`.
2. Route supported sales operations to `SalesAnalysisService`.
3. Pass only verified structured results to the mock chat client for executive wording.
4. Implement `ForecastingAgent`.
5. Route forecasting requests to `SalesForecastingService`.
6. Include method, historical period, assumptions, and warnings in the result.
7. Use Agent Framework agent types/invocation where practical, but keep orchestration explicit and easy to follow.
8. Add cancellation and controlled exceptions.
9. Do not calculate values in prompts or parse financial values back out of natural-language model output.


## Expected files or areas


- `src/CfoAgent.Api/Agents/SalesAnalysisAgent.cs`
- `src/CfoAgent.Api/Agents/ForecastingAgent.cs`
- supporting prompt templates/instructions


## Acceptance criteria


- Sales agent answers weekly summary, comparison, and top-products requests.
- Forecast agent returns five deterministic forecast years.
- Results include agent name and structured data.
- Mock wording matches supplied values.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not implement the Knowledge Agent or CFO Orchestrator yet.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-009 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
