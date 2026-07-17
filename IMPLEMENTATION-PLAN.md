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
│   ├── CfoAgent.FinanceMcpServer/       # Read-only finance MCP provider
│   └── CfoAgent.KnowledgeFileMcpServer/ # Restricted read-only knowledge-file MCP provider
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

## Phase 4 — MCP integrations — Completed

Implemented two independent process-backed stdio MCP connections using the official MCP C# SDK:

1. **Finance MCP server**
   - Exposes exactly:
     - `get_sales_summary`
     - `compare_sales_periods`
     - `get_top_products`
     - `get_historical_sales`
     - `get_budget_target`
   - Used by the Sales and Forecasting agents when enabled.
   - Preserves deterministic C# calculations and local-service fallback.

2. **Knowledge File MCP server**
   - Exposes exactly:
     - `list_knowledge_files`
     - `read_knowledge_file`
   - Restricted to `data/knowledge`.
   - Rejects absolute paths, traversal, links/junction escapes, and all write/execute operations.
   - Uses the existing restricted in-process reader as fallback.
   - Does not replace ChromaDB; semantic RAG retrieval and citations remain ChromaDB responsibilities.

Both integrations:

- Are disabled by default.
- Start lazily on first use.
- Use finite timeouts and cancellation tokens.
- Propagate caller cancellation without fallback.
- Use controlled fallback when disabled, unavailable, timed out, or capability-deficient.
- Log stable fallback reasons without sensitive values.

**Verified exit gate after `TASK-CFO-017`:**

- Focused filesystem MCP tests: 10 passed.
- All MCP tests: 31 passed.
- All backend/solution tests: 99 passed.
- Serialized full solution build: passed with 0 warnings and 0 errors.
- All original `TASK-CFO-017` acceptance criteria were satisfied.

## Current verified baseline before TASK-CFO-018

- Four .NET projects are present in `CfoAgent.sln`:
  - `CfoAgent.Api`
  - `CfoAgent.Api.Tests`
  - `CfoAgent.FinanceMcpServer`
  - `CfoAgent.KnowledgeFileMcpServer`
- Phase 4 is complete.
- Current checkpoint: 99 tests passing.
- ChromaDB remains the RAG vector store.
- MCP integrations are configuration-controlled, disabled by default, and lazy.
- The application remains one ASP.NET Core business monolith; MCP servers are assignment tool providers, not business microservices.
- Remaining work starts with `TASK-CFO-018` and must not recreate or replace completed MCP functionality.

### Required solution validation commands

A known local parallel MSBuild project-reference race can cause a non-diagnostic failure with the default multi-node solution build. Use serialized validation for all remaining tasks:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Individual project builds may still use their normal commands. Do not treat the serialized build requirement as an application architecture change.

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

- Morning: Phase 4 (completed)
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

## Remaining-task alignment rules

For `TASK-CFO-018` and later tasks:

- Do not recreate either MCP server or MCP client integration.
- Do not replace ChromaDB RAG with raw file reading.
- Preserve the existing MCP fallback, cancellation, timeout, and lazy-start behavior.
- Use the current result contracts rather than creating a second parallel contract.
- Run serialized solution build/test commands shown above.
- Treat 99 passing tests as the minimum checkpoint; later tasks may increase the count.
- Update documentation when externally visible behavior changes.

