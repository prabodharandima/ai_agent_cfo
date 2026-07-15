# TASK-CFO-001 — Create solution, monolithic API, tests, and React app

## Phase

Phase 0 — Repository bootstrap

## Goal

Create the compilable backend and frontend skeletons from an empty repository.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-000` completed.

## Implementation steps


1. Create `CfoAgent.sln`.
2. Create one ASP.NET Core Web API project at `src/CfoAgent.Api` targeting .NET 10.
3. Create one xUnit project at `tests/CfoAgent.Api.Tests` and reference the API project.
4. Add both .NET projects to the solution.
5. Create a React + TypeScript Vite application at `src/cfo-agent-ui`.
6. Add a minimal frontend test setup using Vitest and React Testing Library.
7. Add root scripts or a small `Makefile`/PowerShell scripts for build, test, and run operations; keep them cross-platform where practical.
8. Configure CORS for the local frontend origin through configuration, not hard-coded production policy.
9. Keep the API project as the single backend monolith; create folders only, not class-library layers.


## Expected files or areas


- `CfoAgent.sln`
- `src/CfoAgent.Api`
- `tests/CfoAgent.Api.Tests`
- `src/cfo-agent-ui`
- optional `scripts/`


## Acceptance criteria


- `dotnet build` passes.
- Backend test project executes.
- React application builds.
- Frontend test runner executes at least one starter test.
- No extra backend class-library project exists.


## Validation commands

```bash
dotnet restore
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
cd src/cfo-agent-ui && npm install && npm run build && npm test -- --run
```

## Constraints and non-goals

- Do not add EF Core, agents, Chroma, MCP, or business features yet.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-001 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
