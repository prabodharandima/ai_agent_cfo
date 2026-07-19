# TASK-MCP-REF-002 — Minimal Generic MCP Adapter Design

## Objective

Design the smallest change needed for dynamic discovery and LLM-based MCP tool selection without over-engineering.

## Prerequisite

`TASK-MCP-REF-001` is complete and `docs/MCP-INTEGRATION-CURRENT-STATE.md` exists.

## Scope

Create `docs/MCP-GENERIC-ADAPTER-DESIGN.md`. Do not implement runtime code.

## Target design

Define one small reusable adapter that:

- uses the existing official MCP SDK
- relies on normal SDK initialization
- calls `tools/list`
- caches discovered tools reasonably
- converts approved tools to LLM-compatible definitions
- lets `IChatClient` select a tool
- validates selected tool and arguments
- calls `tools/call`
- refreshes on reconnect
- maps missing/removed tools to controlled dependency failures

## Required decisions

Document:

1. Minimum new interfaces/classes.
2. Current classes retained.
3. Hard-coded mappings removed.
4. Business-routing mappings retained.
5. Whether Finance and Knowledge share the adapter.
6. Allow-list policy.
7. Handling of new read-only tools.
8. Blocking of dangerous tools.
9. Cache and reconnect behavior.
10. Errors and tests.
11. Exact files expected to change.

## Simplicity constraints

- Prefer one adapter and one small options type.
- No registry DB, plugin framework, custom schema engine, or background polling.
- Keep specialist agents unless clearly unnecessary.
- Do not change public APIs or MCP contracts.

## Validation

```bash
git status
git diff --check
```

Confirm only the design document changed. Stop after this task.
