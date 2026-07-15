# TASK-CFO-015 — Phase 3 RAG and orchestration test gate

## Phase

Phase 3 — RAG and orchestration

## Goal

Validate document ingestion, retrieval, Knowledge Agent grounding, and multi-agent routing.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-014` completed.

## Implementation steps


1. Add unit tests for chunking, stable IDs, and deterministic embeddings.
2. Add Chroma integration tests that can be skipped with a clear message when Docker is unavailable.
3. Test retrieval of budget, forecast assumption, risk, and product strategy content.
4. Test insufficient-knowledge behavior.
5. Test all orchestrator intents.
6. Test mixed-agent combination.
7. Assert source metadata is present for RAG answers.
8. Add `docs/PHASE-3-RESULTS.md`.


## Expected files or areas


- `tests/CfoAgent.Api.Tests/Rag/`
- orchestrator tests
- `docs/PHASE-3-RESULTS.md`


## Acceptance criteria


- Offline unit tests pass.
- Chroma integration tests pass when Docker is running.
- All five MVP prompts produce the expected agent routing.
- Phase gate report is complete.


## Validation commands

```bash
docker compose up -d
dotnet test CfoAgent.sln --configuration Release
```

## Constraints and non-goals

- Do not continue to MCP if the required Docker-backed tests fail.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-015 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
