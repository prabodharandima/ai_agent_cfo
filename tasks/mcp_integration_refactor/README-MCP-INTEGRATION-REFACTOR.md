# MCP Integration Refactor Task Pack

## Goal

Re-scan the current CFO AI Agent MCP integration and, only if needed, refactor it to a simple generic MCP tool adapter pattern.

Target flow:

1. Connect to the configured MCP server.
2. Perform normal MCP initialization through the SDK.
3. Discover tools using `tools/list`.
4. Convert approved discovered tools into LLM tool definitions.
5. Let the LLM choose the tool from the user prompt.
6. Validate the selected tool and arguments.
7. Invoke it through `tools/call`.
8. Return controlled failures for unavailable servers or invalid selections.

## Non-goals

- No agent-platform rewrite.
- No registry database, plugin marketplace, workflow engine, policy engine, service bus, or custom MCP protocol.
- No dynamic code generation.
- No unrelated refactoring.
- No replacement of the official MCP SDK.

## Simplicity rules

- Prefer one small reusable adapter abstraction.
- Prefer SDK types over custom wrappers.
- Keep configuration minimal.
- Preserve specialist agents unless clearly unnecessary.
- Preserve public API and MCP contracts.
- Stop after each task.

## Execution order

1. `TASK-MCP-REF-001-current-state-discovery.md`
2. `TASK-MCP-REF-002-minimal-refactor-design.md`
3. `TASK-MCP-REF-003-implement-generic-mcp-tool-adapter.md`
4. `TASK-MCP-REF-004-regression-and-documentation-gate.md`
