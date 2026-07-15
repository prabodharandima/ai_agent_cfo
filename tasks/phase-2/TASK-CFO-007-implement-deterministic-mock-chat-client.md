# TASK-CFO-007 — Implement deterministic Mock IChatClient

## Phase

Phase 2 — Mock LLM and specialist agents

## Goal

Create a mock-only model adapter that can later be replaced without changing agent/business code.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 1 gate passed.

## Implementation steps


1. Add stable `Microsoft.Extensions.AI` packages required for `IChatClient`.
2. Implement `MockChatClient : IChatClient` using the current official interface contract.
3. Support deterministic behaviors:
   - classify request intent,
   - format a sales executive summary from supplied JSON,
   - format a forecast explanation from supplied JSON,
   - format a knowledge answer from retrieved chunks,
   - format an unsupported-question response.
4. Never generate random values.
5. Add optional simulated delay and failure flags through development configuration.
6. Include provider/model metadata showing `Mock`.
7. Register the mock client as the only `IChatClient`.
8. Keep provider selection configuration-ready but do not implement other providers.


## Expected files or areas


- `src/CfoAgent.Api/AI/Mock/`
- `src/CfoAgent.Api/AI/Contracts/` only where needed
- DI registration


## Acceptance criteria


- No network call is made.
- Same input produces same output.
- Mock response never adds financial numbers not present in supplied context.
- Unsupported provider configuration fails early.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not implement Ollama, OpenAI, Azure OpenAI, or Claude.
- Do not create a large custom LLM abstraction that duplicates `IChatClient`.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-007 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
