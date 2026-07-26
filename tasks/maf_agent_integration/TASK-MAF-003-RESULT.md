# TASK-MAF-003 Result - Structured LLM Output

## Previous Changes Detected

- `TASK-MAF-000-RESULT.md`, `TASK-MAF-001-RESULT.md`, and `TASK-MAF-002-RESULT.md` were present before this task.
- The repository was clean before this task began. Earlier Microsoft Agent Framework middleware work was preserved.

## Files Changed

- `src/CfoAgent.Api/Agents/Contracts/StructuredAgentOutputs.cs`
- `src/CfoAgent.Api/Agents/Configuration/AgentPromptTemplates.cs`
- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- `src/CfoAgent.Api/Agents/SalesAnalysisAgent.cs`
- `src/CfoAgent.Api/Mcp/IFinanceMcpClient.cs`
- `src/CfoAgent.Api/Mcp/FinanceMcpClient.cs`
- `tests/CfoAgent.Api.Tests/AI/TestChatClient.cs`
- `tests/CfoAgent.Api.Tests/Agents/StructuredOutputTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/FinanceMcpClientTests.cs`
- `tests/CfoAgent.Api.Tests/PhaseEight/ContractFreezeTests.cs`
- `APPLICATION_ARCHITECTURE.md`

## Implementation

- Added `IntentClassificationOutput` and `SalesSummaryDateRangeOutput` as typed schema outputs.
- `CfoOrchestratorAgent.ClassifyAsync` now requests `ChatResponseFormat.ForJsonSchema<IntentClassificationOutput>` and retains the existing deterministic keyword fallback when JSON is malformed, missing, unknown, or `Unsupported`.
- `SalesAnalysisAgent` retains the existing deterministic current-week path for requests explicitly mentioning `this week` or `current week`.
- Other sales-summary requests request `SalesSummaryDateRangeOutput` through `ChatResponseFormat.ForJsonSchema<T>`. C# requires exact `YYYY-MM-DD` values, rejects future dates and reversed ranges, limits inclusive ranges to 366 days, and then sends canonical `DateOnly` values to `IFinanceMcpClient.GetSalesSummaryAsync`.
- Invalid date output becomes the existing sanitized `AiProviderException` invalid-response path before Finance MCP is called. The model does not calculate financial data.

## Tests And Results

- Focused structured-output, finance-client, affected agent, and MCP-wiring tests: **36 passed, 0 failed**.
- Serialized restore/build: **passed**, 0 warnings, 0 errors.
- Full Debug suite after Docker Desktop was started: **218 passed, 0 failed, 8 skipped** out of 226. The skips are existing opt-in Ollama, ChromaDB, and container-script tests.

## Documentation Updated

- `APPLICATION_ARCHITECTURE.md` now describes schema-bound intent output, deterministic fallback, non-current sales date interpretation, C# validation/canonicalization, and the unchanged fixed Finance MCP tool selection.

## Warnings

- None from the serialized build.

## Blockers

- None.

## Completion Status

Implementation and validation are complete.
