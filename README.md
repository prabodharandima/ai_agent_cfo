# CFO AI Agent

A locally runnable CFO assistant MVP for five finance and planning questions. It combines deterministic C#/SQL calculations, source-grounded financial knowledge retrieval, a React chat interface, and optional read-only MCP tool access. The only model provider is the deterministic Mock LLM; no model credentials are required.

## Scope

The MVP supports these demonstrable scenarios:

1. Current-week sales summary.
2. Week-over-week sales comparison.
3. Current-month top products.
4. Five-year sales forecast.
5. Annual sales target and the assumptions behind it.

Finance values and forecasts are calculated by deterministic C# and SQL code. The Mock LLM classifies supported prompts and writes explanations from verified results; it is never used as a calculation authority. Knowledge answers are retrieved from ChromaDB and retain their document citations.

## Architecture

```mermaid
flowchart LR
    User[Business user] --> UI[React + TypeScript chat UI]
    UI -->|POST /api/chat| API[ASP.NET Core API]

    subgraph Monolith[Single CFO AI Agent monolith]
        API --> Orchestrator[CFO Orchestrator]
        Orchestrator --> Sales[Sales Analysis Agent]
        Orchestrator --> Forecast[Forecasting Agent]
        Orchestrator --> Knowledge[Financial Knowledge Agent]
        Orchestrator --> Mock[Mock IChatClient]
        Sales --> Finance[Deterministic finance services]
        Forecast --> Finance
        Finance --> SQLite[(SQLite demo data)]
        Knowledge --> Retrieval[ChromaDB retrieval]
        Retrieval --> Chroma[(ChromaDB)]
    end

    Sales -. optional, lazy, read-only .-> FinanceMcp[Finance MCP process]
    Forecast -. optional, lazy, read-only .-> FinanceMcp
    Knowledge -. optional, lazy, read-only .-> KnowledgeMcp[Knowledge File MCP process]
    FinanceMcp --> SQLite
    KnowledgeMcp --> Files[data/knowledge Markdown]
```

This is a simple ASP.NET Core monolith: the API owns the business workflow, agents, calculations, data access, fallback decisions, and HTTP surface. The two MCP processes are narrowly scoped local tool providers, not independently deployed business services or microservices. They are disabled by default, start only when first needed, expose only allow-listed read operations, and fall back to existing local implementations when unavailable.

## Prerequisites

- .NET SDK `10.0.302` or a compatible patch selected by [global.json](global.json).
- Node.js 22 or later with npm.
- Docker Desktop running locally for ChromaDB.
- PowerShell on Windows for the supplied scripts, or equivalent shell commands on another platform.

No OpenAI, Azure, Anthropic, or other LLM API key is required.

## Start Locally

From the repository root, use these commands in order. The seed and ingestion commands are repeatable.

```powershell
docker compose up -d
dotnet restore CfoAgent.sln
dotnet run --project src/CfoAgent.Api -- --seed
dotnet run --project src/CfoAgent.Api -- --ingest-rag
dotnet run --project src/CfoAgent.Api
```

In a second terminal, start the React UI:

```powershell
Set-Location src/cfo-agent-ui
npm ci
npm run dev
```

Open `http://localhost:5173`. The API runs on `http://localhost:5260` under the local launch profile. `POST /api/chat` is the only chat endpoint; it accepts `{ "message": "..." }`. Health probes are available at `/health/live` and `/health/ready`.

The default configuration keeps both MCP integrations disabled. The application continues with its local deterministic finance services and direct RAG/document path. The browser test startup script, [start-phase-5-e2e-api.ps1](scripts/start-phase-5-e2e-api.ps1), shows the controlled local configuration used to exercise both optional MCP processes.

## Demo Prompts

Run these in the UI:

1. `Give me the sales summary of this week.`
2. `Compare this week's sales with last week.`
3. `Show me the top five products this month.`
4. `Give me the sales forecast for the next five years.`
5. `What is the annual sales target and what assumptions were used?`

The sales responses show KPIs or product data, the forecast shows a deterministic forecast table and chart, and the target response includes knowledge citations. A complete 10-15 minute walkthrough is in [docs/DEMO-SCRIPT.md](docs/DEMO-SCRIPT.md).

## Test And Validation

Run the serialized solution commands because the local environment requires one MSBuild node for reliable project-reference builds:

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Run the UI checks from `src/cfo-agent-ui`:

```powershell
npm ci
npm run build
npm test -- --run
npm run test:e2e
```

For a clean-state release validation, follow the exact recorded commands and results in [docs/FINAL-VALIDATION.md](docs/FINAL-VALIDATION.md). `docker compose down -v` removes only the local ChromaDB volume; rerun seed and ingestion afterward.

## MCP And Fallbacks

- Finance MCP allows five read-only finance tools. Sales and Forecasting agents use it only when explicitly enabled and available; otherwise they use the existing local finance services.
- Knowledge File MCP allows only listing and reading files rooted under `data/knowledge`. It cannot write, execute, traverse directories, or access outside that root. It does not replace ChromaDB semantic retrieval or citations.
- Both clients validate capabilities, use timeouts, propagate caller cancellation, dispose processes safely, and log stable fallback reasons without configuration values or document contents.

See [docs/PHASE-4-RESULTS.md](docs/PHASE-4-RESULTS.md) and [docs/SECURITY-NOTES.md](docs/SECURITY-NOTES.md) for the MCP validation and security controls.

## Intentional MVP Limits

- Mock LLM only; no real-provider credentials, streaming, or autonomous behavior.
- No authentication, authorization, persistent chat history, or multi-user tenancy.
- SQLite and a local ChromaDB container are development/demo dependencies.
- Forecasting uses a transparent deterministic trend approach, not a production statistical model.
- The application handles only the five defined CFO intents.

To introduce a real model provider later, add an `IChatClient` implementation and change dependency-injection registration while retaining the agents' verified-result contracts. Keep deterministic finance calculations out of the model call. Production alternatives for embeddings, vector storage, relational data, and operations are deliberately documented rather than implemented in [docs/FUTURE-IMPROVEMENTS.md](docs/FUTURE-IMPROVEMENTS.md).

## Reviewer Notes

- [Demo script](docs/DEMO-SCRIPT.md)
- [Architecture trade-offs](docs/TRADE-OFFS.md)
- [Future improvements, not implemented](docs/FUTURE-IMPROVEMENTS.md)
- [Final validation record](docs/FINAL-VALIDATION.md)
- [Security notes](docs/SECURITY-NOTES.md)
