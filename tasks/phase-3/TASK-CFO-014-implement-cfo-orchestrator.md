# TASK-CFO-014 — Implement CFO Orchestrator Agent

## Phase

Phase 3 — RAG and orchestration

## Goal

Create the main agent that routes CEO questions to one or more specialist agents and combines their structured results.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Sales, Forecasting, and Knowledge agents completed.

## Implementation steps


1. Implement a small intent model: sales summary, sales comparison, top products, forecast, knowledge, mixed, unsupported.
2. Use the mock chat client or a deterministic classifier backed by it to classify the prompt.
3. Route to one specialist for simple intents.
4. For five-year forecast questions requiring assumptions, invoke Forecasting and Knowledge agents and combine results.
5. Preserve all sources, assumptions, warnings, structured data, and participating-agent names.
6. Add a final mock-model formatting step using only specialist outputs.
7. Set a maximum number of specialist invocations per request.
8. Do not create autonomous loops, reflection agents, planners, or recursive agent calls.


## Expected files or areas


- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- intent/routing types


## Acceptance criteria


- All five MVP questions route correctly.
- Mixed forecast/assumption questions use two specialist agents.
- Unsupported questions get a safe scoped response.
- No recursion or unbounded execution is possible.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Keep orchestration explicit and deterministic.
- Do not add another agent.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-014 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
