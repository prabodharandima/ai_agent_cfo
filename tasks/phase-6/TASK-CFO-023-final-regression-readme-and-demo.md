# TASK-CFO-023 — Final regression, README, and interview demo

## Phase

Phase 6 — Hardening and submission

## Goal

Prepare a reproducible final submission and a concise interviewer demonstration.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-022` completed.

## Implementation steps


1. Run all backend, frontend, integration, and E2E tests.
2. Verify setup from a clean local state.
3. Complete root README with:
   - purpose and scope,
   - prerequisites,
   - one-command or minimal startup,
   - seed and RAG ingestion,
   - running tests,
   - five sample prompts,
   - limitations.
4. Add architecture diagrams using Mermaid.
5. Explain why this is a monolith and why MCP servers do not make it microservices.
6. Document mock LLM replacement path using `IChatClient`.
7. Document production alternatives for ChromaDB and deterministic embeddings.
8. Add `docs/DEMO-SCRIPT.md` for a 10–15 minute walkthrough.
9. Add `docs/TRADE-OFFS.md` and `docs/FUTURE-IMPROVEMENTS.md`.
10. Remove dead code, placeholder TODOs, unused packages, and accidental secrets.
11. Create `docs/FINAL-VALIDATION.md` with exact command results.


## Expected files or areas


- root `README.md`
- `docs/DEMO-SCRIPT.md`
- `docs/TRADE-OFFS.md`
- `docs/FUTURE-IMPROVEMENTS.md`
- `docs/FINAL-VALIDATION.md`


## Acceptance criteria


- Clean build and all tests pass.
- A reviewer can start the complete system from README instructions.
- Demo covers all five MVP prompts.
- Architecture and trade-offs are clearly explained.
- No real LLM credential is required.


## Validation commands

```bash
docker compose down -v
docker compose up -d
dotnet restore
dotnet build CfoAgent.sln --configuration Release
dotnet test CfoAgent.sln --configuration Release
cd src/cfo-agent-ui && npm ci && npm run build && npm test -- --run && npm run test:e2e
```

## Constraints and non-goals

- Do not add new features during finalization. Fix only defects, documentation gaps, and reproducibility issues.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-023 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
