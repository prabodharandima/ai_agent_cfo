# Security Notes

- Prompts are untrusted. Routing is limited to the five supported intents, and model providers receive bounded verified data or retrieved context only.
- Finance MCP exposes five allow-listed read-only tools and is the sole PostgreSQL owner. API failures are sanitized Problem Details responses without SQL, stack traces, credentials, or connection strings.
- Knowledge File MCP exposes only list/read operations beneath a read-only `data/knowledge` mount. Absolute paths, traversal, links/junction escapes, writes, and execution are rejected.
- Docker keeps PostgreSQL, ChromaDB, and both MCP ports internal. MCP authentication is intentionally deferred from this local Phase 8 deployment.
- Logs record stable dependency outcomes and correlation IDs, not raw prompts, document content, finance rows, SQL, or secrets.
