# MVP Assumptions

- The project remains a local interview MVP with one ASP.NET Core business monolith and four in-process agents.
- Finance MCP exclusively owns PostgreSQL, migrations, deterministic seeding, and finance SQL. The API does not retain finance persistence or a Finance fallback.
- Finance and Knowledge File MCP services use internal Streamable HTTP; PostgreSQL and MCP ports are not published.
- ChromaDB stores only indexed Markdown knowledge chunks and citation metadata. It remains the semantic retrieval system.
- Finance values and forecasts remain deterministic C# or SQL results. Mock is the default offline provider; optional Ollama is not a calculation authority.
- Knowledge local fallback is Development-only and explicit. Container deployments disable it and mount `data/knowledge` read-only.
