# Implementation Plan

## Target repository structure

```text
/
├── AGENT.md
├── CfoAgent.sln
├── Directory.Build.props
├── docker-compose.yml
├── README.md
├── data/
│   ├── knowledge/
│   └── imports/
├── src/
│   ├── CfoAgent.Api/               # Single ASP.NET Core monolith
│   │   ├── Agents/
│   │   ├── AI/
│   │   ├── Controllers/
│   │   ├── Data/
│   │   ├── Features/
│   │   ├── Mcp/
│   │   ├── Models/
│   │   ├── Rag/
│   │   ├── Services/
│   │   └── Program.cs
│   └── cfo-agent-ui/               # React + TypeScript
├── tools/
│   └── CfoAgent.FinanceMcpServer/  # Small external tool provider, not a business microservice
└── tests/
    ├── CfoAgent.Api.Tests/
    └── CfoAgent.E2E/
```

## Phase 0 — Repository bootstrap

Create the solution, monolithic API, React application, tests, configuration, local ChromaDB, and health checks.

**Exit gate:** backend and frontend start successfully; ChromaDB is reachable.

## Phase 1 — Structured finance data

Create the SQLite schema, seed realistic sample data, implement deterministic sales calculations and forecasting, then test them.

**Exit gate:** all finance service tests pass with a fixed clock.

## Phase 2 — Mock LLM and specialist agents

Implement a provider-agnostic mock `IChatClient`, configure Microsoft Agent Framework, and create Sales and Forecasting agents.

**Exit gate:** deterministic agent tests pass without any real model or network LLM call.

## Phase 3 — RAG and orchestration

Create deterministic embeddings, ChromaDB ingestion/retrieval, the Knowledge Agent, and CFO Orchestrator.

**Exit gate:** the orchestrator can answer all five MVP prompts using the correct agents and sources.

## Phase 4 — MCP integrations

Create one small Finance MCP server and connect a read-only filesystem MCP server. Integrate MCP client calls into the monolith with safe fallback to local services.

**Exit gate:** MCP tools can be listed and invoked; failures return controlled errors or fall back as specified.

## Phase 5 — Chat API and React UI

Expose the chat endpoint and implement a simple chat experience with KPI cards, a forecast table/chart, assumptions, warnings, and sources.

**Exit gate:** a browser user can complete the five MVP scenarios.

## Phase 6 — Hardening and submission

Add validation, exception handling, logs, final regression tests, setup documentation, architecture notes, production alternatives, and a demonstration script.

**Exit gate:** clean clone setup works and all validation commands pass.

## Suggested two-day schedule

### Day 1

- Morning: Phases 0 and 1
- Early afternoon: Phase 2
- Late afternoon: Phase 3

### Day 2

- Morning: Phase 4
- Early afternoon: Phase 5
- Late afternoon: Phase 6 and demo rehearsal

## Scope controls

Do not add:

- Authentication or user management
- Microservices or message brokers
- A generic plugin framework
- Multiple relational databases
- Generic repository/unit-of-work wrappers around EF Core
- CQRS/MediatR unless already required by `AGENT.md`
- Streaming responses
- Long-term chat memory
- Background job frameworks
- Advanced model evaluation
- Real LLM providers
- Cloud deployment

## Production discussion only

The README may explain future replacements:

- Mock `IChatClient` → Ollama, Azure OpenAI, OpenAI, or Claude adapter
- Deterministic embedding generator → production embedding model
- ChromaDB → Azure AI Search or PostgreSQL/pgvector
- SQLite → Azure SQL or PostgreSQL
- Local files → Blob Storage or controlled document platform
