# Manual Test Cases

These checks exercise the current Phase 8 deployment: React/Nginx, the ASP.NET Core API, Finance MCP with PostgreSQL, Knowledge File MCP, and ChromaDB.

## Prerequisites

- Docker Desktop is running with Linux containers enabled.
- The .NET SDK selected by `global.json` is installed for backend validation.
- Node.js 22 or later is installed for local frontend validation.
- Copy `.env.example` to `.env` for a fresh checkout. Install Ollama on the host, pull `llama3.2:3b`, and retain `AI_MODEL=llama3.2:3b` unless deliberately using another locally installed model.

## Start The Deployment

From the repository root:

```powershell
docker compose up --build -d
docker compose ps --all
```

Wait for `postgres`, `finance-mcp`, `knowledge-mcp`, `chromadb`, `api`, and `frontend` to become healthy. `finance-db-init` and `rag-init` should complete with `Exited (0)`.

Verify the public endpoints:

```powershell
Invoke-WebRequest http://localhost:5260/health/live
Invoke-WebRequest http://localhost:5260/health/ready
Invoke-WebRequest http://localhost:5173/health
```

All three requests should return HTTP 200. Open `http://localhost:5173` for the manual cases below.

## TC-01: Weekly Sales Summary

**Steps**

1. Select `Give me the sales summary of this week.`
2. Wait for the response.

**Expected behavior**

- A sales-summary response displays verified revenue and order metrics.
- The response identifies the Sales Analysis Agent.
- No provider badge, stack trace, SQL, or internal endpoint is displayed.

## TC-02: Week-Over-Week Comparison

**Steps**

1. Select `Compare this week's sales with last week.`
2. Wait for the comparison result.

**Expected behavior**

- Current-week and prior-week values are displayed with the calculated change.
- The values are deterministic Finance MCP SQL results, not LLM calculations.

## TC-03: Monthly Top Products

**Steps**

1. Select `Show me the top five products this month.`
2. Review the rendered table.

**Expected behavior**

- A ranked top-products table appears.
- The response is attributed to the Sales Analysis Agent.
- Finance data remains available only through Finance MCP; PostgreSQL and MCP ports are not host-published.

## TC-04: Five-Year Forecast

**Steps**

1. Select `Give me the sales forecast for the next five years.`
2. Review the chart and forecast table.

**Expected behavior**

- The forecast table and chart display five future years.
- Historical totals come from Finance MCP; forecast arithmetic remains deterministic C# behavior.

## TC-05: Knowledge And Citations

**Steps**

1. Select `What is the annual sales target and what assumptions were used?`
2. Review the answer and sources.

**Expected behavior**

- The answer contains grounded planning information and a Sources section.
- Citations come from ChromaDB semantic retrieval; raw Knowledge File MCP access does not replace RAG citations.

## TC-06: Dependency Health And Boundaries

**Steps**

1. Run `docker compose ps`.
2. Confirm that only ports `5173` and `5260` have host mappings.
3. Inspect API logs with `docker compose logs --no-color api`.

**Expected behavior**

- Finance MCP capability discovery reports five tools.
- Knowledge File MCP capability discovery reports two read-only tools.
- PostgreSQL, ChromaDB, and both MCP services remain internal to Docker.

## Stop The Deployment

```powershell
docker compose down
```

This preserves PostgreSQL and ChromaDB named volumes. Use `docker compose down -v` only when intentionally resetting local data.
