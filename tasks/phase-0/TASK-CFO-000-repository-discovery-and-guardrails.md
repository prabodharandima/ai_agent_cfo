# TASK-CFO-000 — Repository discovery and guardrails

## Phase

Phase 0 — Repository bootstrap

## Goal

Prepare the empty repository for safe incremental Codex execution without creating application code yet.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Empty project folder containing `AGENT.md` and this task pack.

## Implementation steps


1. Read `AGENT.md` and summarize its binding architecture/domain rules in `docs/DECISIONS.md`.
2. Create `.gitignore`, `.editorconfig`, `Directory.Build.props`, and `global.json`.
3. Target `net10.0`, nullable reference types, implicit usings, warnings enabled, and deterministic builds.
4. Create root folders: `src`, `tests`, `tools`, `data/knowledge`, `data/imports`, and `docs`.
5. Create `docs/ASSUMPTIONS.md` recording MVP scope, mock-only LLM, monolith boundary, and two-day constraint.
6. Create `docs/DEFINITION-OF-DONE.md` with build/test/documentation expectations.
7. Do not create solution or application projects in this task.


## Expected files or areas


- `.gitignore`
- `.editorconfig`
- `global.json`
- `Directory.Build.props`
- `docs/DECISIONS.md`
- `docs/ASSUMPTIONS.md`
- `docs/DEFINITION-OF-DONE.md`


## Acceptance criteria


- Repository conventions are present and understandable.
- `docs/DECISIONS.md` is consistent with `AGENT.md`.
- No application code or unnecessary framework has been introduced.


## Validation commands

```bash
dotnet --info
git status --short
```

## Constraints and non-goals

- Do not create the .NET solution, React app, database, agents, or Docker services.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-000 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
