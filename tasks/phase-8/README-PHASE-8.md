# Phase 8 — Containerized MCP and PostgreSQL Refactor

## Target architecture

```text
Browser
  |
React/Nginx container
  |
CfoAgent.Api container
  |-------------------------------|
  |                               |
Finance MCP container       Knowledge File MCP container
  |                               |
PostgreSQL container        read-only knowledge volume

CfoAgent.Api container --> ChromaDB container
CfoAgent.Api container --> Ollama on Windows host via host.docker.internal
```

## Binding decisions

- Two separate MCP containers.
- Replace SQLite with PostgreSQL.
- PostgreSQL is owned and used only by Finance MCP.
- `CfoAgent.Api` must not connect directly to PostgreSQL.
- Remove the local Finance fallback; Finance MCP failures return controlled dependency errors.
- Knowledge File MCP may retain a secure configuration-controlled development fallback.
- Container integration tests disable local fallbacks.
- Use network MCP transport suitable for containers, preferably Streamable HTTP through the official MCP C# SDK.
- Keep ChromaDB as the semantic RAG store.
- Keep Ollama on the Windows host.
- No MCP authentication in Phase 8; MCP ports remain internal by default.
- Containerize the frontend only after backend/container integration is complete.

## Execution order

1. Architecture discovery and migration design
2. Contract freeze and baseline gate
3. Move finance ownership to Finance MCP
4. PostgreSQL migrations and deterministic seeding
5. Finance MCP Streamable HTTP host
6. Knowledge File MCP Streamable HTTP host
7. API HTTP MCP clients and failure policy
8. Remove finance persistence from API
9. Backend Dockerfiles and Docker Compose
10. Container integration and resilience gate
11. Frontend container and full local deployment
12. Documentation, cleanup, and Phase 8 gate

## Standard validation

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```
