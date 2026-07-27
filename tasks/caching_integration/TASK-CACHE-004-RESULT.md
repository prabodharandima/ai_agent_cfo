# TASK-CACHE-004 Result

## Earlier changes detected

- TASK-CACHE-001 is complete and committed as `d266185`.
- TASK-CACHE-002 is complete and committed as `a207bea`.
- TASK-CACHE-003 is complete and committed as `6386eff`.
- The provider-neutral cache, Finance/RAG/embedding caching, optional Redis backing, and MCP allow-lists were retained.

## Implementation

- Preserved `McpToolAdapter`'s connection-local approved-tool-name set and lazy SDK client.
- Added a shared `IApplicationCache` snapshot containing only sorted approved tool names.
- New API instances can reuse a valid snapshot after performing the required MCP initialization handshake, avoiding a repeated `tools/list`.
- MCP calls now use the live SDK client's `CallToolAsync`; no MCP SDK object is serialized or exposed outside infrastructure.
- Cached names are filtered through the current allow-list again when loaded.
- Missing required allow-listed tools trigger one direct refresh and still fail with `CapabilityMismatch` when absent.
- Transport timeouts, unavailable transport, and MCP tool errors reset the client, remove the shared snapshot, and force the next discovery to bypass distributed cache once.
- Cache read/write/removal failures fail open to direct `tools/list`. Caller cancellation remains cancellation and is not cached.
- Tool results, arguments, schemas, endpoints, secrets, and prompts are not cached.

## Cache key and TTL

The key shape is:

`mcp-discovery:{schemaVersionSha256}:{dependencyIdentitySha256}:{endpointSha256}:{sortedAllowListSha256}`

- Dependency identity distinguishes Finance MCP from Knowledge File MCP.
- The canonical Streamable HTTP `/mcp` endpoint is hashed rather than stored in the key.
- Sorting before hashing makes allow-list order irrelevant while any membership change creates a new key.
- `Cache:McpDiscovery:SchemaVersion` defaults to `v1`.
- `Cache:McpDiscovery:TtlSeconds` defaults to 300 seconds.
- Compose maps `CACHE_MCP_DISCOVERY_SCHEMA_VERSION` and `CACHE_MCP_DISCOVERY_TTL_SECONDS`.

## Tests and validation

- Focused `McpToolAdapterTests`: 11 passed, 0 failed, 0 skipped.
- Broader MCP client and HTTP integration focus with Docker: 31 passed, 0 failed, 0 skipped.
- Solution restore and build: succeeded with 0 warnings and 0 errors.
- Full suite with Docker access: 259 passed, 7 failed, 8 skipped; 274 total.
- `git diff --check`: passed.

The seven full-suite failures are the same baseline agent-test inconsistencies documented by TASK-CACHE-002 and TASK-CACHE-003: one obsolete structured-output marker expectation, three obsolete exception-type expectations, and three stale prompt or cancellation call-count expectations. They are unrelated to MCP discovery caching.

## Documentation

Updated `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, `IMPLEMENTATION-PLAN.md`, and `README.md` with connection-local versus shared discovery caching, safe keys, TTL/version configuration, allow-list enforcement, and failure-driven refresh.

## Completion

TASK-CACHE-004 is complete. TASK-CACHE-005 was not started.
