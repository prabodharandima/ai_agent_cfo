# TASK-CACHE-004 — Cache MCP Tool Discovery

## Goal
Cache approved MCP tool discovery while preserving allow-lists and existing connection/reset behavior.

## Requirements
First inspect existing in-memory discovery caching. Do not duplicate it blindly. Add distributed caching only where useful across reconnects or API instances.

Key must include:
- MCP server/dependency identity;
- stable endpoint identifier or URL hash;
- allow-list hash/version;
- discovery schema version.

Never bypass allow-lists. Missing required tools must still fail. Transport or capability failures must invalidate/bypass cached discovery. Redis failure must call `tools/list`. Do not cache tool results in this task. Do not expose MCP SDK types outside infrastructure.

## Focused tests
1. Repeated discovery can use cache.
2. Changed allow-list uses a different key.
3. Missing required tool is rejected.
4. Transport/capability failure forces refresh.
5. Cache failure falls back to `tools/list`.
6. Cancellation is not cached.

Run focused tests, build, full tests, and `git diff --check`. Document connection-local versus distributed discovery caching. Create `TASK-CACHE-004-RESULT.md`.
