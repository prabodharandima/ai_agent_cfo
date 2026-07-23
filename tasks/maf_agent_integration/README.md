# Microsoft Agent Framework Integration Task Pack

## Status and scope

This is a **planned enhancement**. The current application does not yet use Microsoft Agent Framework packages or APIs. The existing `CfoAgent.Api` request flow, `IChatClient`/Ollama integration, typed MCP facades, ChromaDB retrieval, and deterministic financial calculations remain the source of truth until each later task is implemented and validated.

The intended features are bounded agent middleware, structured LLM output for classification and sales-summary date ranges, an optional streaming endpoint, bounded in-memory sessions, RAG context-provider integration, and safe optional OpenTelemetry. They must not be described as implemented in current-state architecture documentation before their individual task is complete.

Package names, versions, and exact framework APIs are **TBA - verify during TASK-MAF-001**. The chosen package set must support `net10.0` and coexist with `Microsoft.Extensions.AI.Abstractions` 10.8.0 and the current Ollama `IChatClient` adapter. Compatibility risks include incompatible chat abstractions, duplicate error/cancellation handling, changes to public chat behavior, and accidentally introducing live-provider requirements into normal automated tests.

Execute the tasks in order, using one Codex session per task.

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
