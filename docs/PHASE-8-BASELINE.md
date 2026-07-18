# Phase 8 Contract Freeze Baseline

## Scope

This baseline is established by `TASK-CFO-P8-002-contract-freeze-and-baseline-gate` before the Phase 8 persistence and transport migration. It freezes externally meaningful behavior without changing the current stdio transport, SQLite implementation, Docker configuration, or public chat API.

## Verified baseline

| Configuration | Total | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: | ---: |
| Debug | 162 | 158 | 4 | 0 |
| Release | 162 | 158 | 4 | 0 |

The four skipped tests are opt-in `OllamaLiveIntegrationTests`. They remain excluded from the deterministic offline gate unless their documented environment opt-in is supplied.

## Frozen Finance MCP contract

The approved read-only Finance MCP tool set is fixed to these exact names:

| Tool | Inputs | Successful typed result | Controlled validation behavior |
| --- | --- | --- | --- |
| `get_sales_summary` | `startDate`, `endDate` in `YYYY-MM-DD` | `FinanceMcpResult<McpSalesSummary>` | Invalid date format or a period longer than 366 days returns `isSuccess: false`. |
| `compare_sales_periods` | `currentStartDate`, `currentEndDate`, `previousStartDate`, `previousEndDate` | `FinanceMcpResult<McpPeriodComparison>` | Each period uses the sales-summary date rules. |
| `get_top_products` | `startDate`, `endDate`, optional `limit` | `FinanceMcpResult<McpTopProducts>` | `limit` must be from 1 through 20. |
| `get_historical_sales` | `startYear`, `endYear` | `FinanceMcpResult<McpHistoricalSales>` | Inclusive range is 2000 through 2100 and at most five years. |
| `get_budget_target` | `year`, optional `month` | `FinanceMcpResult<McpBudgetTarget>` | Year is 2000 through 2100; provided month is 1 through 12. |

The envelope property names are `isSuccess`, `data`, and `error`. Finance tool results are mapped by `FinanceMcpClient` to the API typed contracts in `src/CfoAgent.Api/Features/Sales/SalesAnalysisResults.cs`:

- `SalesSummary`
- `WeeklySalesComparison`
- `TopProductsResult`
- `HistoricalYearlySalesResult`
- `BudgetTargetResult`

`IFinanceMcpClient` method names, parameter types, and return types are frozen by `ContractFreezeTests`. The tool catalog, MCP attribute names, input parameter names, valid typed outputs, camel-case JSON result properties, and controlled validation error text are also covered there.

Finance MCP must remain read-only from the API perspective. No write, mutation, arbitrary tool selection, or LLM-calculated finance values are part of this contract.

## Frozen Knowledge File MCP contract

The approved restricted-file tool set is fixed to:

| Tool | Inputs | Successful typed result |
| --- | --- | --- |
| `list_knowledge_files` | `cancellationToken` | `KnowledgeFileMcpResult<IReadOnlyList<string>>` |
| `read_knowledge_file` | `relativePath`, `cancellationToken` | `KnowledgeFileMcpResult<string>` |

The server permits only files below its configured knowledge root. The following behavior is frozen:

- allowed file listing and reading succeeds;
- a missing file returns `isSuccess: false` with a controlled error;
- traversal such as `../outside.md` is rejected;
- absolute paths are rejected; and
- no write, rename, move, delete, execute, or directory-creation operation is exposed.

The Knowledge File MCP is not a replacement for ChromaDB. It supplies restricted file access only; semantic retrieval, source metadata, and citations remain the responsibility of `FinancialKnowledgeRetrievalService` and ChromaDB.

## Frozen chat/API contract

`POST /api/chat` continues to support exactly these MVP request scenarios and response types:

| Scenario | Expected `responseType` |
| --- | --- |
| Weekly sales summary | `sales_summary` |
| Week-over-week sales comparison | `sales_comparison` |
| Current-month top products | `top_products` |
| Five-year sales forecast | `forecast` |
| Annual target and assumptions | `knowledge` |

For successful responses, the following fields remain present: `conversationId`, `answer`, `agentNames`, `responseType`, `structuredData`, `sources`, `assumptions`, `warnings`, `dataPeriod`, and `model`. The orchestrator name remains `CfoOrchestratorAgent` and default provider metadata remains `Mock`.

Knowledge answers retain a citation with source path `data/knowledge/current-budget-and-target.md`. Response payloads must not expose `CfoAgent.Api.Models` entities.

Invalid messages remain HTTP 400 Problem Details. A controlled agent/dependency failure remains a sanitized HTTP 503 Problem Details response with:

```json
{
  "status": 503,
  "title": "CFO assistant is temporarily unavailable.",
  "type": "https://httpstatuses.com/503"
}
```

The response includes a trace ID but does not expose exception messages or stack traces. Phase 8 will use this shape for Finance MCP dependency failure; caller cancellation remains distinct and must not be converted into a fallback or HTTP 503.

## Deterministic calculation and RAG protections

- `SalesAnalysisService` currently supplies deterministic finance values from SQLite. Its query behavior is protected while ownership moves to Finance MCP.
- `SalesForecastingService` uses deterministic ordinary least-squares regression and produces ordered conservative, expected, and optimistic rows. The forecast contract is `SalesForecastResult` and `SalesForecastRow`.
- The deterministic forecast tests protect the historical period, five forecast years, scenario ordering, and repeatability.
- `RagDocumentIngestionService`, `DeterministicTokenHashEmbeddingGenerator`, and `FinancialKnowledgeRetrievalService` retain ChromaDB chunk metadata, retrieval, and unique citations.
- `MockChatClient` and `OllamaChatClient` remain the only `IChatClient` providers. The selected provider is metadata only; neither provider calculates authoritative financial values.

## Contract-freeze test inventory

New or strengthened coverage is in:

- `tests/CfoAgent.Api.Tests/PhaseEight/ContractFreezeTests.cs`
- `tests/CfoAgent.Api.Tests/Api/ChatApiTests.cs`

Existing complementary coverage remains in:

- `tests/CfoAgent.Api.Tests/Mcp/FinanceMcpProcessTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/KnowledgeFileMcpProcessTests.cs`
- `tests/CfoAgent.Api.Tests/Finance/PhaseOneFinanceServiceTests.cs`
- `tests/CfoAgent.Api.Tests/FinanceAnalysisAndForecastingTests.cs`
- `tests/CfoAgent.Api.Tests/Rag/Retrieval/FinancialKnowledgeRetrievalTests.cs`
- `tests/CfoAgent.Api.Tests/AI/AiProviderRegistrationTests.cs`

## Phase 8 migration guardrails

Later Phase 8 tasks may replace SQLite with PostgreSQL, move finance persistence into Finance MCP, and replace stdio with network transport. They must preserve the frozen tool names, input names, result envelopes, API typed result contracts, chat response types, deterministic forecast behavior, citations, provider metadata, and sanitized error shape unless a later task explicitly introduces a compatible contract change with replacement coverage.
