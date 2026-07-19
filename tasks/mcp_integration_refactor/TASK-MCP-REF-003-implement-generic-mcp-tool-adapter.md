# TASK-MCP-REF-003 — Implement Generic MCP Tool Adapter

## Objective

Implement the approved minimal generic MCP tool adapter and use dynamic discovery plus LLM tool selection.

## Prerequisites

- Tasks 001 and 002 complete
- `docs/MCP-GENERIC-ADAPTER-DESIGN.md` exists and matches the repository

## Required behavior

1. Use the existing official MCP SDK.
2. Initialize normally.
3. Discover with `tools/list`.
4. Cache discovered tools.
5. Convert approved tools to LLM tool definitions using SDK support where possible.
6. Let the configured `IChatClient` choose the tool.
7. Validate selected tool, approval, and arguments.
8. Invoke through `tools/call`.
9. Preserve existing response composition.
10. Refresh discovery on reconnect.
11. Return controlled dependency failures for removed tools or server outages.
12. Permit newly discovered approved read-only tools without new hard-coded client methods.
13. Preserve cancellation, sanitized 503 behavior, Mock, and Ollama support.

## Refactor rules

- Remove only hard-coded MCP mappings replaced by the adapter.
- Keep business-level routing where useful.
- Do not expose every tool blindly.
- Use a simple allow-list.
- Do not add unnecessary abstractions or frameworks.
- Do not change MCP tool contracts, public APIs, PostgreSQL, ChromaDB, or Docker ownership.

## Tests required

Prove:

- `tools/list` discovery
- approved discovered tools reach the LLM
- LLM-selected tool is called through `tools/call`
- a newly discovered approved tool works without a new hard-coded method
- removed tools fail safely
- unapproved tools are blocked
- invalid arguments are rejected
- cancellation propagates
- outage maps to existing dependency errors
- existing Finance and Knowledge scenarios still pass
- Mock remains deterministic

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
git diff --check
```

Run existing Docker MCP integration tests if available. Stop after this task.
