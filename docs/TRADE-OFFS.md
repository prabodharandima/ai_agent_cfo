# Architecture Trade-Offs

`CfoAgent.Api` remains a simple monolith because orchestration, agents, deterministic forecasting, RAG, and the chat API change together. Finance MCP and Knowledge File MCP are separate only where data ownership and restricted tool boundaries require it.

PostgreSQL is owned by Finance MCP, which removes API database coupling but makes Finance MCP an explicit availability dependency. That is intentional: the API returns a controlled 503 rather than stale or locally divergent finance data.

ChromaDB remains separate from file access because semantic retrieval and citations need vector search, while Knowledge MCP is a narrow filesystem safety boundary. Mock remains the default provider; host Ollama is optional and cannot calculate finance values.
