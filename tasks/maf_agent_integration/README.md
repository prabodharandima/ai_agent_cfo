# Microsoft Agent Framework Integration Task Pack

## Status and scope

This task pack is **complete**. `CfoAgent.Api` uses `Microsoft.Agents.AI` 1.13.0 with `Microsoft.Extensions.AI.Abstractions` 10.8.0 on `net10.0`. The verified integration is deliberately bounded: `AgentChatMiddleware` wraps the existing `IChatClient`; structured output is limited to intent classification and sales-summary date ranges; `POST /api/chat/stream` is a separate compatible SSE endpoint; sessions retain bounded metadata only; `FinancialKnowledgeContextProvider` prepares bounded transient RAG context; and `AgentTelemetry` exposes safe OpenTelemetry-compatible signals.

The integration does not change the authoritative architecture. `CfoAgent.Api` remains the business orchestrator; typed MCP facades and allow-lists retain deterministic Finance tool routing; ChromaDB remains the semantic retrieval and citation source; and deterministic C# and SQL calculations remain authoritative. Normal tests use test-local `IChatClient` doubles and do not require live Ollama.

The result files record the validated implementation and regression evidence for each task.

## Order

1. `TASK-MAF-000-update-plan-documents.md`
2. `TASK-MAF-001-discovery-and-plan.md`
3. `TASK-MAF-002-agent-middleware.md`
4. `TASK-MAF-003-structured-output.md`
5. `TASK-MAF-004-streaming-responses.md`
6. `TASK-MAF-005-agent-sessions.md`
7. `TASK-MAF-006-rag-context-provider.md`
8. `TASK-MAF-007-opentelemetry.md`
9. `TASK-MAF-008-regression-and-final-docs.md`

## Execution rules

- Use one new Codex session per task.
- Use the same repository and branch.
- Commit each completed task before starting the next one.
- Treat repository code as the source of truth.
- Read earlier completed task result files before starting a later task.
- Do not perform unrelated refactoring.
- Keep deterministic finance calculations, typed MCP routing, validation, authorization, and allow-lists unchanged.
- Update planning documents before coding.
- Update architecture documents only after implementation and tests confirm the actual behavior.
- Keep the existing `POST /api/chat` contract unless a later task explicitly adds a separate compatible endpoint.
- Preserve cancellation and controlled dependency-failure behavior; framework features must not turn either into silent fallback behavior.
- Keep normal automated tests offline. Live Ollama validation, if needed, must be opt-in and clearly separated.

Each implementation task must create a matching result file:

`TASK-MAF-XXX-RESULT.md`
