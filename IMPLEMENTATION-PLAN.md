# Implementation Plan

Phases 0 through 7 established the monolithic API, deterministic finance behavior, Mock/Ollama `IChatClient` providers, ChromaDB RAG, the chat UI, and hardening.

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

The current Streamable HTTP clients use one minimal generic `McpToolAdapter` for SDK initialization, `tools/list` discovery, approved-tool caching, bounded tool definition forwarding to `IChatClient`, selected-name/canonical-argument validation, and `tools/call`. Tool discovery is constrained by configured Finance and Knowledge allow-lists; a model cannot choose an arbitrary endpoint, tool, or financial argument. The existing four-agent routing and typed Finance/Knowledge facades remain to protect deterministic result contracts, filesystem restrictions, and ChromaDB RAG behavior. Results and validation evidence are recorded in `docs/MCP-INTEGRATION-REFACTOR-RESULTS.md`.

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
