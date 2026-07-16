# CFO AI Agent MVP — Codex Implementation Pack

This pack breaks the CFO AI Agent MVP into small, sequential tasks suitable for execution by Codex AI Agent.

## Fixed decisions

- Build within two working days with AI-assisted development.
- Use **.NET 10 / ASP.NET Core** for the backend.
- Use a **single monolithic backend project**. Do not create Domain, Application, Infrastructure, or feature class-library projects.
- Use **React + TypeScript + Vite** for the frontend.
- Use **Microsoft Agent Framework** for the agent objects/workflow.
- Use a deterministic **Mock LLM** through `Microsoft.Extensions.AI.IChatClient`.
- Do not integrate Ollama, OpenAI, Azure OpenAI, or Claude in this version.
- Use **SQLite** for structured sales, product, and budget data.
- Use **ChromaDB** in Docker for RAG.
- Use a deterministic local embedding implementation for the mock phase.
- Use the official **MCP C# SDK** for MCP client/server integration.
- Keep the business application monolithic. MCP servers are external tool integrations, not decomposed business microservices.
- Do not add authentication, authorization, queues, Redis, Kubernetes, microservices, or advanced production infrastructure.

## Main MVP use cases

1. “Give me the sales summary of this week.”
2. “Compare this week’s sales with last week.”
3. “Show the top five products this month.”
4. “Give me the sales forecast for the next five years.”
5. “What is the annual sales target and what assumptions were used?”

## Agents

- **CFO Orchestrator Agent** — routes the request and combines specialist results.
- **Sales Analysis Agent** — handles actual historical sales and KPIs.
- **Forecasting Agent** — produces deterministic five-year forecasts.
- **Financial Knowledge Agent** — retrieves relevant company documents through RAG.

## Important accuracy rule

The Mock LLM must not invent financial values. C# services, SQL queries, MCP tools, and the forecasting service calculate the values. The mock chat client only simulates intent selection and executive-friendly wording.

## How to use this pack

1. Copy this directory into the empty project folder.
2. Ensure the project root contains `AGENT.md`.
3. Execute `TASK-CFO-000` first.
4. Execute task files in the order listed in `EXECUTION-ORDER.md`.
5. Give Codex only one task at a time.
6. Do not move to the next phase until its validation task passes.

## Local startup

Start ChromaDB with its persisted local Docker volume:

```powershell
docker compose up -d
```

Restore and run the API in the development environment:

```powershell
dotnet restore
dotnet run --project src/CfoAgent.Api
```

Initialize or safely re-run the deterministic demo seed without deleting existing data:

```powershell
dotnet run --project src/CfoAgent.Api -- --seed
```

Ingest the local Markdown knowledge documents into ChromaDB:

```powershell
dotnet run --project src/CfoAgent.Api -- --ingest-rag
```

The API exposes these local operational endpoints:

- `http://localhost:5260/` identifies the application and the Mock provider.
- `http://localhost:5260/health/live` confirms the API process is live.
- `http://localhost:5260/health/ready` reports ChromaDB readiness without exposing internal errors.
- `http://localhost:5260/openapi/v1.json` is available in development.

Stop ChromaDB when it is no longer needed:

```powershell
docker compose down
```
