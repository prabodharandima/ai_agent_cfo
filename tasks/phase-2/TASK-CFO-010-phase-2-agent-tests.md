# TASK-CFO-010 — Phase 2 agent test gate

## Phase

Phase 2 — Mock LLM and specialist agents

## Goal

Validate the mock provider and the first two agents without network dependencies.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-009` completed.

## Implementation steps


1. Test deterministic `MockChatClient` behavior.
2. Test intent-classification outputs used by the current phase.
3. Test sales agent with fixed time and seeded data.
4. Test forecast agent output, assumptions, and warnings.
5. Test cancellation and simulated mock failure.
6. Assert that natural-language answers contain calculated values supplied by services.
7. Add `docs/PHASE-2-RESULTS.md`.
8. Fix defects without introducing later-phase capabilities.


## Expected files or areas


- `tests/CfoAgent.Api.Tests/AI/`
- `tests/CfoAgent.Api.Tests/Agents/`
- `docs/PHASE-2-RESULTS.md`


## Acceptance criteria


- Tests run fully offline.
- All agent tests pass.
- No HTTP request is made to a real model.
- Phase gate report is complete.


## Validation commands

```bash
dotnet test CfoAgent.sln --configuration Release
```

## Constraints and non-goals

- Do not continue to RAG if this gate fails.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-010 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
