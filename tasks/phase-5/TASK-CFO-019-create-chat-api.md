# TASK-CFO-019 — Create chat API

## Phase

Phase 5 — Chat API and React UI

## Goal

Expose the CFO Orchestrator through one clear HTTP contract.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 4 gate passed.

## Implementation steps


1. Create `POST /api/chat`.
2. Request contains `conversationId` (optional for first message) and `message`.
3. Response contains:
   - conversation ID,
   - answer,
   - agent names,
   - response type,
   - structured KPI/forecast payload,
   - sources,
   - assumptions,
   - warnings,
   - data period.
4. Add request validation and bounded message length.
5. Add a lightweight in-memory conversation store only if Agent Framework requires session continuity.
6. Do not persist chat history to the database.
7. Map known failures to suitable Problem Details.
8. Add OpenAPI examples for the five MVP prompts.


## Expected files or areas


- `src/CfoAgent.Api/Controllers/ChatController.cs` or minimal API equivalent
- `src/CfoAgent.Api/Features/Chat/`
- API tests


## Acceptance criteria


- All MVP prompts work through HTTP.
- Invalid requests return validation Problem Details.
- No entity/internal model is leaked.
- Response supports both plain text and UI visualizations.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not add authentication, streaming, WebSockets, or database chat storage.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-019 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
