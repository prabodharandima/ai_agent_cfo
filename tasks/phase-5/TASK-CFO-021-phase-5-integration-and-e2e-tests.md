# TASK-CFO-021 — Phase 5 integration and E2E test gate

## Phase

Phase 5 — Chat API and React UI

## Goal

Verify the full user journey from browser to agents, SQLite, ChromaDB, and MCP tools.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-020` completed.

## Implementation steps


1. Create Playwright configuration under `tests/CfoAgent.E2E` or the frontend.
2. Add a reliable startup script for ChromaDB, MCP server, API, and frontend.
3. Test:
   - weekly summary,
   - week comparison,
   - top products,
   - five-year forecast,
   - annual target/assumptions.
4. Assert important values, agent labels, and sources.
5. Test invalid prompt and dependency failure UI.
6. Capture screenshots or traces only on failure.
7. Add `docs/PHASE-5-RESULTS.md`.


## Expected files or areas


- Playwright/E2E setup
- integration test startup scripts
- `docs/PHASE-5-RESULTS.md`


## Acceptance criteria


- All five browser scenarios pass.
- Tests use deterministic data and fixed demo time.
- Failures produce useful diagnostics.
- Phase gate report is complete.


## Validation commands

```bash
docker compose up -d
dotnet test CfoAgent.sln --configuration Release
cd src/cfo-agent-ui && npm test -- --run && npm run test:e2e
```

## Constraints and non-goals

- Do not rely on a real LLM or internet access.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-021 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
