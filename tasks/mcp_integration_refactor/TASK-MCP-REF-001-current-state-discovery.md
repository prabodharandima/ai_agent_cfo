# TASK-MCP-REF-001 — Current-State MCP Integration Discovery

## Objective

Inspect and document how MCP initialization, discovery, selection, and invocation currently work. This is discovery-only.

## Mandatory reading

Read completely:

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- `EXECUTION-ORDER.md`
- `tasks/mcp_integration_refactor/README-MCP-INTEGRATION-REFACTOR.md`
- `tasks/mcp_integration_refactor/MCP-INTEGRATION-REFACTOR-EXECUTION-ORDER.md`
- this task file

## Scope

Inspect:

- CFO orchestrator and intent classification
- specialist agents
- Finance and Knowledge MCP clients
- MCP SDK initialization
- `tools/list` and `tools/call`
- tool caching and validation
- allow-lists
- LLM tool definitions
- argument binding
- errors, health checks, DI, configuration, and tests

Create `docs/MCP-INTEGRATION-CURRENT-STATE.md`.

## Questions to answer

1. Is the MCP handshake performed by the SDK?
2. Is `tools/list` called?
3. Are tools cached?
4. Does the LLM receive discovered tools?
5. Does the LLM choose the final MCP tool?
6. Which selections are hard-coded business logic?
7. What happens when tools are added, removed, or their schemas change?
8. Are selected tools and arguments validated?
9. Are unexpected tools filtered?
10. Is the current code already close enough to a generic adapter?

For every conclusion include path, class/method, evidence, behavior, and risk.

## Rules

- Do not modify runtime code.
- Distinguish agent routing from MCP tool selection.
- Verify source rather than trusting documentation.

## Validation

```bash
git status
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
git diff --check
```

Confirm only the discovery document changed. Stop after this task.
