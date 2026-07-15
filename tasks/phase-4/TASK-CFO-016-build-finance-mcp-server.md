# TASK-CFO-016 — Build the small Finance MCP server

## Phase

Phase 4 — MCP integrations

## Goal

Create one narrowly scoped read-only Finance MCP tool server for demonstration.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 3 gate passed.

## Implementation steps


1. Create `tools/CfoAgent.FinanceMcpServer` as a .NET 10 console/tool project.
2. Add it to the solution under a `tools` solution folder.
3. Use the official MCP C# SDK.
4. Connect read-only to the same development SQLite database.
5. Expose only these tools:
   - `get_sales_summary`
   - `compare_sales_periods`
   - `get_top_products`
   - `get_historical_sales`
   - `get_budget_target`
6. Validate all inputs and limit date ranges/result sizes.
7. Return typed JSON results and controlled errors.
8. Use stdio transport for the MVP unless `AGENT.md` requires otherwise.
9. Add a tool-list smoke command/test.


## Expected files or areas


- `tools/CfoAgent.FinanceMcpServer/`
- solution update
- MCP tool tests where practical


## Acceptance criteria


- Server starts locally.
- MCP client can list the five tools.
- Tools return deterministic read-only results.
- No write/delete tool exists.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
dotnet run --project tools/CfoAgent.FinanceMcpServer
```

## Constraints and non-goals

- This tool server is an external integration boundary, not a business microservice.
- Do not move domain logic out of the monolith.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-016 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
