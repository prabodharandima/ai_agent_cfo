# Architecture And Domain Decisions

- `CfoAgent.Api` is the single business monolith. Its four agents run in process.
- Finance MCP and Knowledge File MCP are the only approved hosted integration services. They are unauthenticated and internal-network only in Phase 8.
- Finance MCP owns PostgreSQL persistence, migrations, seed data, aggregation, and budget lookup. Finance outages are sanitized dependency failures, normally HTTP 503; cancellation is propagated.
- Knowledge MCP exposes only read-only list/read operations below `data/knowledge`. ChromaDB remains RAG retrieval and citation authority.
- Mock and optional Ollama are the only `IChatClient` providers. No cloud provider, dynamic tool selection, or LLM financial calculation is allowed.
- React/Nginx serves the browser UI and proxies same-origin `/api` to the API. PostgreSQL, ChromaDB, and MCP services remain unpublished.
