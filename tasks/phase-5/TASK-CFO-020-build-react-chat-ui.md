# TASK-CFO-020 — Build the React TypeScript CFO chat UI

## Phase

Phase 5 — Chat API and React UI

## Goal

Create a single-page executive chat experience for the five MVP scenarios.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-019` completed.

## Implementation steps


1. Build one responsive chat page.
2. Include:
   - application title and mock-mode badge,
   - example prompt buttons,
   - conversation messages,
   - prompt textbox and send button,
   - loading and error states.
3. Add a typed API client.
4. Render structured outputs:
   - KPI cards for summaries,
   - comparison indicators,
   - top-products table,
   - five-year forecast table and one simple chart,
   - assumptions, warnings, and sources.
5. Preserve conversation ID in component state only.
6. Add accessible labels and keyboard submission.
7. Keep styling simple; do not introduce a design system unless already specified by `AGENT.md`.


## Expected files or areas


- `src/cfo-agent-ui/src/features/chat/`
- reusable display components
- typed API models
- frontend tests


## Acceptance criteria


- User can run all five prompts.
- All response types render without console errors.
- Empty, loading, failure, and source states are visible.
- UI clearly says the model is mocked.


## Validation commands

```bash
cd src/cfo-agent-ui && npm run build && npm test -- --run
```

## Constraints and non-goals

- Do not add login, routing beyond the single page, global state libraries, or complex theming.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-020 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
