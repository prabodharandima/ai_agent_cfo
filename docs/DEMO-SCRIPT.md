# CFO AI Agent Demo Script

Start the complete application with `docker compose up --build -d`, then open `http://localhost:5173`.

1. Ask for this week's sales summary. Point out the Sales Analysis Agent and deterministic KPI values returned through Finance MCP.
2. Ask for the week-over-week comparison and current-month top products. These are deterministic SQL-backed Finance MCP results, not LLM arithmetic.
3. Ask for the five-year forecast. Historical totals come from Finance MCP; forecast regression and scenario calculations remain deterministic C# in the API.
4. Ask for annual target assumptions. Point out Financial Knowledge Agent citations from ChromaDB. Knowledge File MCP only validates restricted file access; it does not replace semantic retrieval.
5. Open `/health/ready` or `docker compose ps` to show healthy containers. Explain that only UI/API ports are published, while MCP, PostgreSQL, and ChromaDB remain internal.
6. Explain resilience: a Finance MCP outage returns sanitized HTTP 503 with no local Finance fallback. Caller cancellation remains distinct. Knowledge fallback exists only when explicitly enabled in Development.
