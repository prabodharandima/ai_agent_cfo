# TASK-CFO-008 — Configure Microsoft Agent Framework and shared agent contracts

## Phase

Phase 2 — Mock LLM and specialist agents

## Goal

Introduce Microsoft Agent Framework with a minimal set of agent contracts and result types.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-007` completed.

## Implementation steps


1. Add current stable Microsoft Agent Framework packages compatible with `IChatClient`.
2. Verify package/API usage against official Microsoft documentation at execution time.
3. Define a small `AgentRequest` and `AgentResult` contract.
4. `AgentResult` should support:
   - plain answer,
   - structured payload,
   - agent names,
   - sources,
   - assumptions,
   - warnings,
   - data period.
5. Create agent names and concise system instructions.
6. Add a shared guardrail instruction: never invent finance values; use supplied tool/service data.
7. Register framework services and conversation/session handling only to the minimum required for a single browser session.
8. Do not add a generic workflow engine or persistent memory.


## Expected files or areas


- `src/CfoAgent.Api/Agents/Contracts/`
- `src/CfoAgent.Api/Agents/Configuration/`
- Agent Framework registration in the monolith


## Acceptance criteria


- Application builds using the current Agent Framework API.
- The mock `IChatClient` is accepted by the agent framework.
- Agent contracts are small and provider-independent.
- No real model credentials are required.


## Validation commands

```bash
dotnet restore
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not implement specialist agents in this task.
- Do not add multi-agent frameworks other than Microsoft Agent Framework.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-008 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
