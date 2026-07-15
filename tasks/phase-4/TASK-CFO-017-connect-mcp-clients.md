# TASK-CFO-017 — Connect Finance and filesystem MCP servers

## Phase

Phase 4 — MCP integrations

## Goal

Connect the monolith to two narrowly scoped MCP servers without changing the core monolithic architecture.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-016` completed.

## Implementation steps


1. Add MCP client support using the official C# SDK.
2. Configure and start/connect to the Finance MCP server using a safe process definition.
3. Configure a standard read-only filesystem MCP server restricted to `data/knowledge`.
4. At startup or first use, list tools/resources and validate required capabilities.
5. Add `FinanceMcpClient` methods for the five finance tools.
6. Add `KnowledgeFileMcpClient` methods for listing/reading only permitted files.
7. Integrate MCP into Sales and Forecasting agents behind a configuration flag.
8. Keep local service fallback enabled for demo resilience and log when fallback is used.
9. Do not let arbitrary user text become a file path or SQL statement.


## Expected files or areas


- `src/CfoAgent.Api/Mcp/`
- MCP configuration
- agent integration updates


## Acceptance criteria


- Both MCP connections can be established in local development.
- Finance tools can be invoked through the monolith.
- Filesystem access cannot escape the knowledge directory.
- Controlled fallback works when an MCP process is unavailable.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not add more MCP servers.
- Do not expose shell execution, unrestricted filesystem, or direct SQL tools.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-017 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
