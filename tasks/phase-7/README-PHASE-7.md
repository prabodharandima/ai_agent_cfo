# Phase 7 — Ollama Integration Task Pack

This task pack adds an optional local Ollama provider using `llama3.2:3b`.

## Binding scope

- Keep the ASP.NET Core application as a simple monolith.
- Retain the deterministic Mock provider.
- Add Ollama as another configuration-selected `IChatClient` provider.
- Keep deterministic C#/SQL financial calculations authoritative.
- Preserve the existing Finance MCP and Knowledge File MCP integrations.
- Preserve ChromaDB and the current embedding implementation.
- Do not add OpenAI, another LLM provider, model-driven MCP execution, new agents, microservices, streaming, or persistent chat history.
- Normal automated tests must remain offline and deterministic.
- Real Ollama tests must be opt-in.

## Execution order

1. `TASK-CFO-P7-001-ollama-discovery-and-design.md`
2. `TASK-CFO-P7-002-ollama-options-and-registration.md`
3. `TASK-CFO-P7-003-ollama-chat-client.md`
4. `TASK-CFO-P7-004-agent-integration-and-guardrails.md`
5. `TASK-CFO-P7-005-resilience-health-and-logging.md`
6. `TASK-CFO-P7-006-offline-test-gate.md`
7. `TASK-CFO-P7-007-live-ollama-integration-tests.md`
8. `TASK-CFO-P7-008-documentation-and-phase-gate.md`

## Standard solution validation

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```
