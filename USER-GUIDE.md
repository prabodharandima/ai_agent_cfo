# CFO AI Agent User Guide

This guide starts the complete application from a fresh local checkout, explains how to verify every container, and shows the normal frontend and validation workflows.

## 1. What runs where

The recommended local deployment uses Docker Compose:

| Component | Purpose | Host access |
|---|---|---|
| Frontend | React application served by Nginx | http://localhost:5173 |
| API | Chat API and four in-process agents | http://localhost:5260 (diagnostic) |
| Finance MCP | Read-only finance tools over Streamable HTTP | Internal only |
| Knowledge File MCP | Restricted read-only knowledge-file tools | Internal only |
| PostgreSQL | Finance products, sales, and budget targets | Internal only |
| ChromaDB | Semantic knowledge retrieval and citations | Internal only |

Finance MCP is the only PostgreSQL owner. The API does not connect directly to PostgreSQL. ChromaDB provides semantic RAG retrieval; Knowledge File MCP does not replace it.

## 2. Prerequisites

Install and start the following before continuing:

- Docker Desktop with Linux containers enabled.
- .NET SDK selected by `global.json` for backend validation.
- Node.js 22 or later with npm for local frontend development and frontend tests.
- PowerShell on Windows for the supplied scripts.

Ollama is optional. The default provider is the deterministic offline Mock LLM, so no model download, API key, or Ollama installation is required for the standard deployment.

Confirm Docker Desktop is healthy:

```powershell
docker version
docker info
```

## 3. Configure the Docker deployment

Docker Compose reads its runtime settings from the root `.env` file. The local `.env` file is ignored by Git because it may contain a PostgreSQL password and local environment choices. The tracked [`.env.example`](.env.example) is the safe starting point for a fresh clone:

```powershell
Copy-Item .env.example .env
```

Open `.env` and set the values you need. Important settings include:

| Setting | Purpose |
|---|---|
| `AI_PROVIDER` / `AI_MODEL` | Select `Mock` / `DeterministicMock` or `Ollama` / `llama3.2:3b` |
| `OLLAMA_BASE_URL` | Host address used by API containers; normally `http://host.docker.internal:11434` |
| `POSTGRES_*` and `FINANCE_DATABASE_CONNECTION_STRING` | Local Finance MCP PostgreSQL database settings |
| `CFO_UI_PORT` / `CFO_API_PORT` | Published frontend and diagnostic API ports |
| `FINANCE_MCP_*` / `KNOWLEDGE_MCP_*` | Internal MCP endpoints, enabled flags, and timeouts |
| `CHROMA_*` / `RAG_*` | ChromaDB and ingestion settings |

The committed `.env.example` selects Mock. Set the following before starting Compose to use host Ollama:

```env
AI_PROVIDER=Ollama
AI_MODEL=llama3.2:3b
```

For direct `dotnet run` development, .NET does not automatically load `.env`; use `appsettings.json`, user secrets, or normal `AI__...` environment variables instead.

## 4. Start everything with Docker

From the repository root:

```powershell
docker compose up --build -d
```

The first run builds the API, MCP, and frontend images. Compose then starts PostgreSQL, ChromaDB, both MCP services, a one-shot finance migration/seed job, a one-shot RAG ingestion job, the API, and the frontend.

Open the application at:

```text
http://localhost:5173
```

The API is also published for diagnostics at:

```text
http://localhost:5260
```

The browser normally calls the API through the frontend's same-origin `/api` proxy, so use the frontend URL for manual testing.

## 5. Rebuild and deploy the latest code

After changing application, MCP, Docker, or frontend code, run this from the repository root:

```powershell
docker compose up --build -d
```

This rebuilds images whose inputs changed, recreates the affected containers, and starts the updated deployment. It preserves the PostgreSQL and ChromaDB named volumes, so deterministic seed data and indexed knowledge remain available.

Use this when you want every service recreated even if Docker believes the image did not change:

```powershell
docker compose up --build --force-recreate -d
```

Rebuild only the service you changed when a full rebuild is unnecessary:

```powershell
# Backend API
docker compose up --build -d api frontend

# Finance MCP
docker compose up --build -d finance-mcp

# Knowledge File MCP
docker compose up --build -d knowledge-mcp

# React/Nginx frontend
docker compose up --build -d frontend
```

If a Docker layer cache is suspected, rebuild without cache before deploying:

```powershell
docker compose build --no-cache
docker compose up -d
```

Do not use `docker compose down -v` as part of a normal redeploy. It removes the PostgreSQL and ChromaDB volumes. After deploying, verify health with the next section before using the UI.

## 6. Verify the containers

Run:

```powershell
docker compose ps --all
```

Expected state:

| Service | Expected state |
|---|---|
| `postgres` | `healthy` |
| `finance-db-init` | `Exited (0)` |
| `finance-mcp` | `healthy` |
| `knowledge-mcp` | `healthy` |
| `chromadb` | `healthy` |
| `rag-init` | `Exited (0)` |
| `api` | `healthy` |
| `frontend` | `healthy` |

`finance-db-init` exits successfully after applying migrations and deterministic seed data. `rag-init` exits successfully after indexing the Markdown documents in `data/knowledge` into ChromaDB. Those two services are expected to stop after successful completion.

Check application health directly:

```powershell
Invoke-WebRequest http://localhost:5260/health/live
Invoke-WebRequest http://localhost:5260/health/ready
Invoke-WebRequest http://localhost:5173/health
```

Each command should return HTTP 200. `/health/live` confirms the API process is running. `/health/ready` confirms the API can reach its configured dependencies, including both MCP services and ChromaDB.

## 7. Verify MCP services

MCP, PostgreSQL, and ChromaDB ports intentionally are not published to Windows. This is expected:

```powershell
docker compose ps
```

Only the frontend port `5173` and API diagnostic port `5260` should appear as host mappings. Finance MCP, Knowledge MCP, PostgreSQL, and ChromaDB should show only their internal container ports.

Use API readiness and logs to verify the MCP connections:

```powershell
docker compose logs --no-color api
docker compose logs --no-color finance-mcp
docker compose logs --no-color knowledge-mcp
```

In the API log, look for successful Finance MCP discovery with five tools and Knowledge File MCP discovery with two read-only tools. The approved tools are:

- Finance MCP: `get_sales_summary`, `compare_sales_periods`, `get_top_products`, `get_historical_sales`, and `get_budget_target`.
- Knowledge File MCP: `list_knowledge_files` and `read_knowledge_file`.

The knowledge directory is mounted read-only inside `knowledge-mcp`. It rejects traversal, absolute paths, writes, execution, and access outside the approved root.

## 8. Use the application

Open `http://localhost:5173` and try these prompts:

1. `Give me the sales summary of this week.`
2. `Compare this week's sales with last week.`
3. `Show me the top five products this month.`
4. `Give me the sales forecast for the next five years.`
5. `What is the annual sales target and what assumptions were used?`

Sales, comparison, and top-product values come from deterministic Finance MCP SQL results. The forecast uses Finance MCP historical totals but performs forecasting arithmetic in deterministic C#. The target-and-assumptions answer includes ChromaDB citations.

## 9. Optional local frontend development

Keep the Docker deployment running so the API is available on port `5260`, then open another PowerShell window:

```powershell
Set-Location src/cfo-agent-ui
npm ci
npm run dev
```

Open the Vite URL printed in the terminal, normally `http://localhost:5173`. If port `5173` is already used by the Docker frontend, stop the frontend container first or choose another Vite port:

```powershell
docker compose stop frontend
npm run dev
```

When finished, restart the containerized frontend with:

```powershell
docker compose start frontend
```

## 10. Run validation

Run backend Debug validation from the repository root:

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Run the Release test gate:

```powershell
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
```

Run frontend checks:

```powershell
Set-Location src/cfo-agent-ui
npm ci
npm test -- --run
npm run build
npm run test:e2e:container
```

`test:e2e:container` runs Playwright against the already-running Docker frontend and verifies all five MVP scenarios.
Run it with the default Mock provider. Ollama is supported for manual use, but its response time is not the deterministic timing contract exercised by this browser regression suite.

Run the isolated MCP/container resilience gate from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-phase-8-containers.ps1
```

This script uses a separate Compose project and removes only its own test containers, network, and volumes. It verifies seed data, RAG ingestion, MCP tools, outage handling, cancellation, security restrictions, and recovery.

## 11. Optional Ollama provider

The default provider is Mock. To use Ollama locally, install Ollama on Windows and pull the model manually:

```powershell
ollama pull llama3.2:3b
```

For local API execution, configure:

```powershell
$env:AI__Provider = 'Ollama'
$env:AI__Model = 'llama3.2:3b'
$env:AI__BaseUrl = 'http://localhost:11434'
```

For Docker Compose, set `AI_PROVIDER=Ollama` and `AI_MODEL=llama3.2:3b` in `.env`, then run `docker compose up -d --force-recreate api`. Containers reach host Ollama through `host.docker.internal`. Ollama is never asked to calculate financial values, select MCP tools, or replace ChromaDB retrieval.

## 12. Stop or reset the application

Stop containers but keep PostgreSQL and ChromaDB data:

```powershell
docker compose down
```

Start them again:

```powershell
docker compose up -d
```

To intentionally delete all local PostgreSQL and ChromaDB data:

```powershell
docker compose down -v
```

Use `down -v` only when you really want a clean data reset. The next `docker compose up -d` reruns finance migration/seed and RAG ingestion.

## 13. Troubleshooting

| Problem | What to check |
|---|---|
| A service is not healthy | Run `docker compose ps --all`, then `docker compose logs --no-color <service>` |
| UI does not load | Confirm `frontend` is healthy and open `http://localhost:5173/health` |
| Chat request fails | Check `http://localhost:5260/health/ready` and API/MCP logs |
| Finance request returns HTTP 503 | Confirm `finance-mcp` and `postgres` are healthy. This is intentional controlled behavior; there is no local finance fallback. |
| Target/assumptions request fails | Confirm `knowledge-mcp`, `chromadb`, and successful `rag-init` completion. |
| Port is already in use | Set `CFO_UI_PORT` or `CFO_API_PORT` before starting Compose, for example `$env:CFO_UI_PORT = '5180'`. |
| Docker cannot start | Confirm Docker Desktop is running with `docker version` and `docker info`. |

For architecture detail and final test results, see [README.md](README.md), [APPLICATION_ARCHITECTURE.md](APPLICATION_ARCHITECTURE.md), and [docs/PHASE-8-RESULTS.md](docs/PHASE-8-RESULTS.md).
