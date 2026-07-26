# TASK-MAF-001 Result

## Current Architecture Summary

`ChatEndpoints.HandleAsync` validates `POST /api/chat`, creates or preserves the response-only `conversationId`, and calls `CfoOrchestratorAgent.HandleAsync` with `HttpContext.RequestAborted`. `AgentRequest` currently contains only the message, so there is no in-memory or persistent conversation state.

`CfoOrchestratorAgent` calls `IChatClient.GetResponseAsync` for bounded intent classification, applies its deterministic fallback, routes to the three specialist agents, and delegates mixed-result joining to `AgentResultComposer`. The composer concatenates existing specialist answers and does not make an LLM call.

`SalesAnalysisAgent` and `ForecastingAgent` obtain authoritative data through `IFinanceMcpClient`; `FinancialKnowledgeAgent` obtains sources through `IFinancialKnowledgeSearch`, builds bounded text context locally, and then calls `IChatClient`. All specialist agents also use `IChatClient` only to present verified data. `Program.cs` constructs and registers `OllamaChatClient` as the sole production `IChatClient`; tests replace it with test-project `TestChatClient` instances.

`ChromaFinancialKnowledgeSearch` owns ChromaDB query, threshold, filter, ordering, and duplicate control. `FinancialKnowledgeAgent.BuildBoundedContext` owns the final context-length bound. `RequestCorrelationMiddleware` supplies correlation IDs and safe request-completion logs, but the application has no `ActivitySource`, `Meter`, OpenTelemetry registration, agent middleware, streaming endpoint, or session store. `ApiExceptionHandler` preserves cancellation and maps LLM, MCP, and vector dependency failures to controlled Problem Details responses.

## Package and API Choice

Do not add packages in this task. The recommended starting package for the implementation tasks is:

- `Microsoft.Agents.AI` version `1.13.0`, with its transitive `Microsoft.Agents.AI.Abstractions` dependency at `1.13.0`.

NuGet lists `Microsoft.Agents.AI` 1.13.0 as supporting `net10.0` and requiring `Microsoft.Extensions.AI`/`Microsoft.Extensions.AI.Abstractions` 10.6.0 or later. The API project already references `Microsoft.Extensions.AI.Abstractions` 10.8.0, so the minimum dependency range is compatible. No provider-specific MAF package is recommended: the existing `OllamaChatClient` already implements the `Microsoft.Extensions.AI.IChatClient` boundary.

The verified framework types/features to evaluate in the implementation tasks are:

- `ChatClientAgent` and `AgentSession`/`ChatClientAgentSession` for bounded sessions and framework-run streaming.
- `IChatClient.AsBuilder().Use(...)` for chat-level middleware around the existing provider client.
- `ChatResponseFormat.ForJsonSchema<T>()` for typed, schema-bound classification output where the current `Microsoft.Extensions.AI` API is sufficient.
- `AIContextProvider` for adapting the existing bounded RAG context preparation without replacing `IFinancialKnowledgeSearch`.
- `OpenTelemetryAgent` and its builder extensions for agent tracing.

The exact signatures must be compile-verified against the selected 1.13.0 package in each implementation task. Microsoft Agent Framework evolves quickly; no workflow, provider, hosting, MCP, or vector-data package is planned because those would duplicate the current typed MCP and ChromaDB architecture.

Sources checked: [Microsoft.Agents.AI 1.13.0](https://www.nuget.org/packages/Microsoft.Agents.AI/1.13.0), [Microsoft Agent Framework .NET API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai?view=agent-framework-dotnet-latest), and [middleware documentation](https://learn.microsoft.com/en-us/agent-framework/agents/middleware/).

## Compatibility Risks

- `Microsoft.Agents.AI` has a fast release cadence. Pin one package version for all MAF packages and compile-test each API before relying on it.
- The current agents call `IChatClient` directly. Replacing the business orchestrator with framework workflow orchestration would duplicate routing and violate the bounded deterministic design. Use framework features only at the LLM-facing seam.
- `conversationId` is not propagated into `AgentRequest` or a session store today. Task 005 must add a bounded application-owned session map without changing the public request or response shape.
- The current sales-summary path always calls `GetCurrentWeekSummaryAsync`; it has no date-range interpretation implementation. Task 003 must treat this as a new bounded behavior authorized by its task, retain C# validation and typed Finance MCP calls, and must not assume an existing resolver can be migrated.
- `OllamaChatClient.GetStreamingResponseAsync` currently produces one update after a complete response. Task 004 must verify real provider/framework streaming behavior while retaining a non-streaming default and safe cancellation.
- RAG context is currently an untrusted, bounded string built in `FinancialKnowledgeAgent`; a context provider must not persist raw retrieved content in a session, bypass relevance/citation checks, or add unrestricted tools.
- Existing `ApiExceptionHandler` and `RequestCorrelationMiddleware` already own HTTP failure and correlation behavior. MAF middleware and telemetry must not duplicate or override them.

## Recommended Implementation Order

1. Task 002: add the smallest chat-level middleware integration around the existing `IChatClient`; reuse `RequestCorrelationMiddleware` rather than creating a second HTTP correlation layer.
2. Task 003: add typed output only for classification and the explicitly scoped sales-date interpretation path, preserving deterministic fallback and validation.
3. Task 004: expose a separate SSE endpoint that consumes a verified streaming seam while leaving `POST /api/chat` unchanged.
4. Task 005: introduce bounded in-memory session ownership keyed by the existing conversation ID.
5. Task 006: move only RAG context acquisition/preparation into an `AIContextProvider` adapter while retaining `IFinancialKnowledgeSearch` and citation assembly.
6. Task 007: add safe optional OpenTelemetry after the request and provider paths are stable.
7. Task 008: run Debug, Release, container, and smoke gates and update current-state documentation from verified behavior.

## Likely Files and Focused Tests by Later Task

| Task | Likely files | Focused tests |
|---|---|---|
| 002 Middleware | `Program.cs`; new narrowly scoped files under `AI/` or `Observability/`; `OllamaChatClientTests.cs`; new middleware tests | invocation, correlation metadata, safe logs, prompt-injection handling, redaction, cancellation, unchanged finance flow |
| 003 Structured output | `CfoOrchestratorAgent.cs`; `AgentPromptTemplates.cs`; likely typed records under `Agents/Contracts`; `SalesAnalysisAgent.cs` and typed finance client files only if the authorized date-range behavior requires them; agent/MCP tests | valid and malformed intent/date responses, deterministic fallback, date validation, no MCP call after invalid input, cancellation |
| 004 Streaming | `ChatEndpoints.cs`; `ChatContracts.cs`; `Program.cs`; API tests | SSE event order, safe stages/content, completion metadata, cancellation, dependency failure, unchanged `POST /api/chat` |
| 005 Sessions | `ChatEndpoints.cs`; `AgentRequest.cs`; a small session store/options type; `Program.cs`; API tests | same-ID continuity, isolation, expiry, message limit, missing ID, cancellation, no raw RAG/secrets retained |
| 006 RAG context | `FinancialKnowledgeAgent.cs`; a small `AIContextProvider` adapter; `Program.cs`; RAG tests | relevant retrieval, bounded context, citation preservation, suspicious retrieved instructions, insufficient knowledge without LLM, cancellation |
| 007 OpenTelemetry | `Program.cs`; `Observability/`; configuration only if exporters/options are needed; observability tests | spans/metrics, safe attributes, failure and cancellation outcome, exporter-disabled behavior |
| 008 Gate | verified active documentation and tests only | full Debug/Release, container/smoke coverage for all supported CFO scenarios and new MAF features |

## Documentation After Verified Implementation

- Task 002: relevant `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, and setup/configuration guidance only if middleware options are added.
- Task 003: actual classification and sales-date interpretation sections in `APPLICATION_ARCHITECTURE.md`.
- Task 004: endpoint, request-flow, and user/setup documentation for the separate streaming endpoint.
- Task 005: session, privacy, configuration, and limitations sections.
- Task 006: actual RAG context flow and limitations sections.
- Task 007: observability and exporter configuration sections.
- Task 008: align `APPLICATION_ARCHITECTURE.md`, `AGENT.md`, `IMPLEMENTATION-PLAN.md`, and active setup documentation with only verified final behavior.

## Deterministic Behavior That Must Remain Unchanged

- The CFO orchestrator remains the business router; no framework workflow, planner, or autonomous tool selection is introduced.
- Finance MCP remains the only finance-data owner. Typed `IFinanceMcpClient` methods, configured allow-lists, canonical arguments, and deterministic C#/SQL values remain authoritative.
- Knowledge MCP remains read-only and filesystem-restricted. ChromaDB remains the retrieval and citation source.
- The LLM never creates finance totals, dates, percentages, rankings, forecasts, MCP endpoint choices, or MCP tool choices.
- `POST /api/chat`, its response contracts, cancellation propagation, Problem Details mapping, and normal offline test behavior remain compatible.

## Blockers

None for this discovery task. The later sales-summary date-range scope and exact 1.13.0 API signatures are explicit implementation-time verification items, not package-install blockers.

## Completion Status

Complete.
