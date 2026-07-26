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

## Microsoft Agent Framework integration - Complete

`CfoAgent.Api` uses `Microsoft.Agents.AI` 1.13.0 with `Microsoft.Extensions.AI.Abstractions` 10.8.0 on `net10.0`. The completed integration keeps the existing architecture explicit and bounded:

1. `AgentChatMiddleware` wraps the composition-root `IChatClient` for prompt-risk checks, safe logging, redaction, timing, and cancellation preservation.
2. Structured model output is used only for intent classification and sales-summary date-range interpretation. C# validates and canonicalizes dates before the typed Finance MCP facade is called.
3. `POST /api/chat/stream` is an optional SSE endpoint; `POST /api/chat` and its JSON contract remain unchanged.
4. `InMemoryAgentSessionStore` keeps bounded response metadata and optional date periods per conversation ID. It never stores prompts, answers, raw RAG content, or MCP data.
5. `FinancialKnowledgeContextProvider` prepares bounded transient RAG context, retains citations and duplicate control, and treats retrieved text as untrusted.
6. `AgentActivityTracing` publishes safe OpenTelemetry-compatible activities and metrics without requiring an exporter.

Normal automated tests use test-local `IChatClient` doubles. Live Ollama tests remain opt-in. The completed task records are under `tasks/maf_agent_integration/`.

The following boundaries remain non-negotiable: deterministic C# and SQL finance values, canonical date validation, typed Finance MCP routing and allow-lists, Knowledge MCP filesystem restrictions, ChromaDB retrieval/citations, cancellation propagation, and sanitized dependency failures.

## Caching integration - In progress

Task 1 adds the provider-neutral `IApplicationCache` port, a HybridCache implementation, optional internal Redis backing, and a decorator that caches every typed Finance MCP read with deterministic safe keys and operation-specific TTLs. Task 2 adds `CachedFinancialKnowledgeSearch`, which caches successful ChromaDB retrieval results using a normalized-question hash, filters, top-K, threshold, and `Rag:IndexVersion`. Local configuration defaults to memory-only caching; Compose enables Redis. Cache failure fails open to the authoritative dependency, while dependency failures, timeouts, and caller cancellation are not cached. Later caching tasks remain unimplemented.

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
