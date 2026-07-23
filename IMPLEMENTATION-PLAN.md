# Implementation Plan

Phases 0 through 7 established the monolithic API, deterministic finance behavior, the provider-neutral `IChatClient` boundary with the current Ollama adapter, ChromaDB RAG, the chat UI, and hardening.

## Phase 8 — Complete

Phase 8 replaced the former local finance persistence and stdio MCP transport with the current container deployment:

1. Finance persistence moved to Finance MCP PostgreSQL with EF Core migrations and deterministic seed data.
2. Finance MCP and Knowledge File MCP became independent Streamable HTTP services using the official C# SDK.
3. API clients use configured internal HTTP endpoints. Finance fallback and all API finance persistence were removed.
4. API failures for unavailable Finance MCP are sanitized dependency errors; caller cancellation remains cancellation.
5. Knowledge fallback is Development-only, explicitly configured, and disabled in container tests.
6. API, MCP services, PostgreSQL, ChromaDB, RAG initialization, and React/Nginx frontend run through Docker Compose.
7. Container integration, outage recovery, browser, Debug, and Release gates are recorded in `docs/PHASE-8-RESULTS.md`.

The solution remains one ASP.NET Core business monolith with four in-process agents. The two hosted MCP services are approved bounded integration services, not per-agent services or a general microservice platform.

## MCP integration refactor - Complete

The current Streamable HTTP clients use one minimal generic `McpToolAdapter` for SDK initialization, `tools/list` discovery, approved-tool caching, requested-operation validation, and `tools/call`. Tool discovery is constrained by configured Finance and Knowledge allow-lists. `FinanceMcpClient` directly invokes the fixed tool for each typed operation with deterministic canonical arguments; a model cannot choose an endpoint, tool, or financial argument. The existing four-agent routing and typed Finance/Knowledge facades remain to protect deterministic result contracts, filesystem restrictions, and ChromaDB RAG behavior. Results and validation evidence are recorded in `docs/MCP-INTEGRATION-REFACTOR-RESULTS.md`.

## CfoAgent.Api architecture refactor - Complete

`CfoAgent.Api` now has one explicit `CfoOrchestratorAgent`, three focused specialist workers, and a concrete deterministic `AgentResultComposer`. HTTP stays in `ChatEndpoints`; the orchestrator only classifies and routes bounded intents; specialists use `IChatClient`, typed MCP ports, or `IFinancialKnowledgeSearch` as appropriate. `McpToolAdapter` owns MCP SDK transport, and `ChromaFinancialKnowledgeSearch` owns the Chroma-backed vector-search adapter. The API has no PostgreSQL dependency, no Ollama-specific agent code, no MCP transport code in agents, and no second LLM composition pass. The completed refactor and final validation evidence are recorded in `docs/CFO-AGENT-API-REFACTOR-RESULTS.md`.

## Microsoft Agent Framework integration - Planned enhancement

The Microsoft Agent Framework work is planned only. No Microsoft Agent Framework package, middleware, structured-output API, streaming endpoint, agent session, RAG context provider, or OpenTelemetry integration is part of the current implementation.

The planned task order is recorded in `tasks/maf_agent_integration/README.md`:

1. Update planning documents.
2. Confirm compatible packages and APIs against the current `net10.0`, `Microsoft.Extensions.AI.Abstractions` 10.8.0, and Ollama implementation.
3. Add bounded agent middleware.
4. Use structured LLM output only for intent classification and sales-summary date-range interpretation.
5. Add an optional, separate streaming endpoint without changing `POST /api/chat`.
6. Add bounded in-memory sessions keyed by the existing conversation ID.
7. Integrate the existing RAG context preparation with a framework context-provider feature.
8. Add safe, optional OpenTelemetry instrumentation.
9. Run regression validation and update current-state documentation only after implementation is verified.

Package names, versions, and exact APIs are **TBA - verify during TASK-MAF-001**. The framework must be compatible with the target framework and current `IChatClient`/Ollama SDK integration; it must not force an incompatible chat-client model, duplicate existing exception handling, or weaken offline tests. Each task must remain independently buildable and testable, use one Codex session and one commit, and create its matching `TASK-MAF-XXX-RESULT.md` file before the next task begins.

The following boundaries remain non-negotiable throughout this planned work: deterministic C# and SQL finance values, canonical date validation, typed Finance MCP routing and allow-lists, Knowledge MCP filesystem restrictions, ChromaDB retrieval/citations, cancellation propagation, and sanitized dependency failures. `APPLICATION_ARCHITECTURE.md` must describe only verified current behavior; planned framework work belongs in this section and the task pack until implemented and tested.

## Validation

Use serialized solution commands:

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
```

For the complete local deployment use `docker compose up --build -d`, then run frontend checks from `src/cfo-agent-ui` and the isolated container gate in `scripts/test-phase-8-containers.ps1`.

## Scope controls

Do not add cloud LLM providers, authentication, streaming, persistent history, arbitrary or unapproved MCP tools, CQRS, MediatR, extra agents, messaging, Kubernetes, or a second business application. Keep finance values deterministic and ChromaDB responsible for semantic RAG citations.
