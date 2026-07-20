# CfoAgent.Api Incremental Refactor Plan

## Purpose and boundaries

This plan implements `docs/CFO-AGENT-API-TARGET-ARCHITECTURE.md` incrementally. Every step must end with a buildable API and passing focused tests before the next step begins.

Behavior that must remain unchanged throughout:

- public `POST /api/chat`, health, root, and Problem Details contracts;
- exactly one orchestrator and three specialist workers;
- five supported CFO scenarios and fixed mixed forecast-plus-knowledge routing;
- Mock as the deterministic default and Ollama as the optional configured provider;
- authoritative finance values from Finance MCP and deterministic C# forecasting only;
- approved MCP tool names, server contracts, canonical arguments, timeout/cancellation, and allow-list security;
- Finance MCP ownership of PostgreSQL and absence of API persistence/fallback;
- ChromaDB semantic retrieval, deterministic embeddings, and citation metadata;
- Knowledge MCP read-only restrictions and Development-only configured local fallback;
- Docker, MCP server, frontend, and PostgreSQL/Chroma ownership boundaries.

## Implementation order

### Step 0 - Reconfirm the baseline and add characterization coverage

**Goal:** Protect the boundaries that will change before removing code.

**Expected test files to modify:**

- `tests/CfoAgent.Api.Tests/Agents/CfoOrchestratorAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/Api/ChatApiTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/McpToolAdapterTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/AgentMcpWiringTests.cs`

**Tests required:**

- record exact LLM calls for single and mixed orchestration;
- prove specialist structured data, sources, assumptions, warnings, and periods survive composition;
- prove typed Ollama, Chroma, and MCP failures retain their type through each specialist;
- prove caller cancellation remains cancellation at endpoint/orchestrator/specialist boundaries;
- prove approved-tool discovery/cache and typed MCP result mappings before changing adapter shape;
- preserve all five HTTP scenario contract assertions.

**Risk/rollback:** Tests may expose current defects described in Task 1. Add target assertions in the same small implementation commit that changes the behavior; do not weaken existing contract/security assertions. Roll back this step as a unit if a public contract changes unexpectedly.

**Gate:** Focused agent/API/MCP tests, serialized solution build, and full backend tests.

### Step 1 - Make result composition single-pass and explicit

**Goal:** Keep one orchestrator while removing the discarded second LLM answer.

**Files to create:**

- `src/CfoAgent.Api/Agents/AgentResultComposer.cs`
- `tests/CfoAgent.Api.Tests/Agents/AgentResultComposerTests.cs`

**Files to modify:**

- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- `src/CfoAgent.Api/Agents/Configuration/AgentPromptTemplates.cs`
- `src/CfoAgent.Api/AI/Mock/MockChatClient.cs`
- `src/CfoAgent.Api/Program.cs`
- `tests/CfoAgent.Api.Tests/Agents/CfoOrchestratorAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/Api/ChatApiTests.cs`

**Implementation:**

1. Add a concrete deterministic composer; do not add an interface.
2. Inject it into the orchestrator.
3. Return one specialist answer unchanged for single-worker intents.
4. Join mixed answers and merge metadata deterministically in stable Forecasting-then-Knowledge order.
5. Remove `ComposeAsync`, `AgentPromptTemplates.ForOrchestration`, and the orchestration marker/Mock branch.
6. Retain `OrchestratedSpecialistResult` only to preserve the existing mixed `structuredData` JSON shape; the composer creates it from full worker results without using it as an LLM prompt payload.

**Tests required:**

- single result is not regenerated and its answer is unchanged;
- mixed results merge all metadata exactly once in stable order;
- mixed structured data keeps both typed worker outputs in the unchanged reduced DTO shape;
- no finance value is calculated or altered by the composer;
- single sales/forecast/knowledge requests perform classification plus one specialist LLM call;
- mixed request performs classification plus two parallel specialist LLM calls and no final LLM call;
- public response types, model metadata, citations, and agent names remain compatible.

**Risk/rollback:** Answer text changes from the former Mock orchestration heading. Public JSON shape must not change. If metadata regression occurs, revert composer/orchestrator together; do not restore a second LLM pass as a patch.

**Gate:** Composer/orchestrator/API focused tests, then full backend tests.

### Step 2 - Narrow specialist boundaries and preserve dependency failures

**Goal:** Remove no-op Knowledge MCP work from semantic queries and make one exception path authoritative.

**Files to modify:**

- `src/CfoAgent.Api/Agents/SalesAnalysisAgent.cs`
- `src/CfoAgent.Api/Agents/ForecastingAgent.cs`
- `src/CfoAgent.Api/Agents/FinancialKnowledgeAgent.cs`
- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- `src/CfoAgent.Api/Features/Chat/ChatEndpoints.cs`
- `src/CfoAgent.Api/Observability/ApiExceptionHandler.cs` only if existing centralized mappings need a compatibility correction
- `tests/CfoAgent.Api.Tests/Agents/PhaseTwoAgentGateTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/AgentMcpWiringTests.cs`
- `tests/CfoAgent.Api.Tests/Api/ChatApiTests.cs`

**Implementation:**

1. Remove `IKnowledgeFileMcpClient`, `IOptions<McpOptions>`, `_knowledgeFileAccessEnabled`, and `RetrieveAsync` preflight logic from `FinancialKnowledgeAgent`.
2. Continue querying Chroma for semantic evidence and citations; Knowledge MCP remains registered and checked operationally.
3. Preserve caller cancellation and rethrow `McpDependencyException`, `ChromaDependencyException`, and `OllamaProviderException` unchanged through workers/orchestrator.
4. Remove the local `InvalidOperationException` catch from `ChatEndpoints`; keep all sanitized translation in `ApiExceptionHandler`.
5. Keep lightweight public-method request guards rather than adding a validation framework.

**Tests required:**

- Knowledge answers use Chroma citations without calling file-list/read;
- Knowledge MCP readiness and secure local fallback tests still pass independently;
- MCP and Chroma failures become sanitized 503 responses with no internal values;
- Ollama timeout remains 504 and unavailability remains 503 when originating inside a specialist;
- caller cancellation is not translated or logged as dependency failure;
- unexpected agent failures preserve the existing sanitized public behavior.

**Risk/rollback:** Prior wiring tests intentionally assert the discarded file preflight and must be replaced with stronger separation tests, not simply deleted. If deployment requires the preflight for an undocumented reason, stop and document the requirement instead of inventing a provenance policy.

**Gate:** Knowledge/observability/API focused tests, readiness tests, then full backend tests.

### Step 3 - Add one vector-search port and move the Chroma adapter

**Goal:** Invert the real vector-store boundary without wrapping `ChromaClient` twice.

**Files to create:**

- `src/CfoAgent.Api/Rag/Retrieval/IFinancialKnowledgeSearch.cs`

**File to move/rename:**

- from `src/CfoAgent.Api/Rag/Retrieval/FinancialKnowledgeRetrievalService.cs`
- to `src/CfoAgent.Api/Rag/Chroma/ChromaFinancialKnowledgeSearch.cs`

The class is renamed from `FinancialKnowledgeRetrievalService` to `ChromaFinancialKnowledgeSearch` and implements `IFinancialKnowledgeSearch`.

**Files to modify:**

- `src/CfoAgent.Api/Agents/FinancialKnowledgeAgent.cs`
- `src/CfoAgent.Api/Program.cs`
- `tests/CfoAgent.Api.Tests/Rag/Retrieval/FinancialKnowledgeRetrievalTests.cs`
- `tests/CfoAgent.Api.Tests/Rag/Chroma/ChromaPhaseThreeIntegrationTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/AgentMcpWiringTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/CfoOrchestratorAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/AI/OllamaLiveIntegrationTests.cs`

**Implementation:**

1. Add the port using existing query/result contracts and cancellation signature.
2. Move/rename the existing retrieval implementation; do not change ranking, thresholds, metadata, or embedding behavior.
3. Register `IFinancialKnowledgeSearch` to `ChromaFinancialKnowledgeSearch`.
4. Inject the port into `FinancialKnowledgeAgent`.
5. Replace direct Chroma setup in agent tests with small search fakes where protocol behavior is irrelevant; keep Chroma adapter tests for protocol behavior.

**Tests required:**

- focused agent test using an `IFinancialKnowledgeSearch` fake;
- adapter tests for collection missing, distance filtering, metadata filters, ordering, deduplication, and cancellation;
- existing ingestion and opt-in Chroma integration tests unchanged in behavior;
- bounded context and citations preserved.

**Risk/rollback:** This is a rename/move, not a retrieval rewrite. If behavior changes, revert the move and interface together. Do not add `IChromaClient` or another mapping layer.

**Gate:** RAG/knowledge tests, serialized build, then full backend tests.

### Step 4 - Simplify LLM and MCP dependency flow

**Goal:** Use standard `IChatClient` directly and remove the redundant LLM tool-confirmation path while preserving MCP security.

**Files to modify:**

- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- `src/CfoAgent.Api/Agents/SalesAnalysisAgent.cs`
- `src/CfoAgent.Api/Agents/ForecastingAgent.cs`
- `src/CfoAgent.Api/Agents/FinancialKnowledgeAgent.cs`
- `src/CfoAgent.Api/Agents/Configuration/AgentPromptTemplates.cs`
- `src/CfoAgent.Api/AI/Mock/MockChatClient.cs`
- `src/CfoAgent.Api/Mcp/IMcpToolAdapter.cs`
- `src/CfoAgent.Api/Mcp/McpToolAdapter.cs`
- `src/CfoAgent.Api/Mcp/FinanceMcpClient.cs`
- `src/CfoAgent.Api/Mcp/KnowledgeFileMcpHttpClient.cs`
- `src/CfoAgent.Api/Health/McpConfigurationHealthCheck.cs`
- `src/CfoAgent.Api/Program.cs`
- `src/CfoAgent.Api/CfoAgent.Api.csproj`
- `tests/CfoAgent.Api.Tests/AgentContractsAndFrameworkTests.cs` before its move below
- `tests/CfoAgent.Api.Tests/Agents/CfoOrchestratorAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/PhaseTwoAgentGateTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/SpecialistAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/McpToolAdapterTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/ApiHttpMcpClientTests.cs`
- `tests/CfoAgent.Api.Tests/AI/MockChatClientGateTests.cs`
- `tests/CfoAgent.Api.Tests/AI/OllamaChatClientTests.cs`

**File to move/rename:**

- from `tests/CfoAgent.Api.Tests/AgentContractsAndFrameworkTests.cs`
- to `tests/CfoAgent.Api.Tests/AgentContractsTests.cs`

Retain its contract assertions and replace framework-session assertions with direct provider/agent prompt assertions.

**File to delete:**

- `src/CfoAgent.Api/Agents/Configuration/CfoAgentFramework.cs`

**Package to remove:**

- `Microsoft.Agents.AI` from `src/CfoAgent.Api/CfoAgent.Api.csproj`, after repository-wide usage confirms no remaining API references.

**Implementation:**

1. Inject `IChatClient` directly into the orchestrator and specialists.
2. Make each bounded model call explicit with the existing agent instructions and prompts; do not add a replacement runner, factory, base class, or extension framework.
3. Remove `SelectMcpToolAsync`, MCP selection prompt markers, canonical-argument parsing in Mock, and hard-coded Mock MCP tool-name selection.
4. Change `IMcpToolAdapter` discovery to expose approved names rather than MCP SDK tool objects; keep SDK objects private in `McpToolAdapter`.
5. Cache the intersection of discovered and allow-listed tools. `CallApprovedToolAsync` validates the requested tool against that cache and invokes it.
6. Have `FinanceMcpClient` call its deterministic expected tool directly with canonical arguments. No model may create or alter those arguments.
7. Make `DiscoverToolsAsync` on each typed remote client explicitly verify that service's complete required contract for readiness. An individual operation validates only its own tool.
8. Preserve reconnect discovery, timeout, cancellation, envelope parsing, disposal, logging, and sanitized exceptions.

**Tests required:**

- Mock and Ollama provider selection still resolves exactly one `IChatClient` and remains lazy;
- all agent instructions are supplied through direct `ChatOptions` and no tool list is supplied during prose generation;
- tools/list is called and approved names are cached;
- SDK tool objects do not cross `IMcpToolAdapter`;
- each typed finance operation invokes its expected tool and exact canonical arguments without an LLM call;
- an unrelated missing allowed tool does not break a healthy operation;
- full readiness is unhealthy when a required service capability is absent;
- unapproved tools remain uncallable;
- schema/server validation failures remain controlled;
- cache resets after timeout/transport/tool failure and rediscovers on reconnect;
- Mock remains deterministic and Ollama configuration/tool-free responses remain supported;
- all five finance/knowledge scenarios and public contracts pass.

**Risk/rollback:** This is the highest-risk step because it touches provider invocation and MCP security. Keep adapter tests green after each subchange. Roll back direct `IChatClient` conversion separately from MCP surface changes if diagnosis requires it; do not reintroduce model-controlled canonical arguments.

**Gate:** Provider tests, MCP adapter/client/readiness tests, all agent/API tests, full backend tests, Release tests, and existing Docker MCP integration tests.

### Step 5 - Remove verified residual complexity

**Goal:** Remove only now-proven dead wrappers and fields.

**Files to modify:**

- `src/CfoAgent.Api/Mcp/KnowledgeFileMcpAccess.cs`
- `src/CfoAgent.Api/Program.cs`
- `src/CfoAgent.Api/Agents/Contracts/AgentRequest.cs`
- `src/CfoAgent.Api/Agents/CfoOrchestratorAgent.cs`
- `src/CfoAgent.Api/Features/Chat/ChatEndpoints.cs`
- `tests/CfoAgent.Api.Tests/Mcp/KnowledgeFileMcpAccessFallbackTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/McpFallbackTests.cs`
- `tests/CfoAgent.Api.Tests/SpecialistAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/CfoOrchestratorAgentTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/OllamaAgentGuardrailTests.cs`
- `tests/CfoAgent.Api.Tests/Agents/PhaseTwoAgentGateTests.cs`
- `tests/CfoAgent.Api.Tests/AI/OllamaLiveIntegrationTests.cs`
- `tests/CfoAgent.Api.Tests/Mcp/AgentMcpWiringTests.cs`
- `tests/CfoAgent.Api.Tests/Rag/Retrieval/FinancialKnowledgeRetrievalTests.cs`

**Files to delete:**

- `src/CfoAgent.Api/Mcp/KnowledgeFileMcpFallback.cs`
- `src/CfoAgent.Api/Mcp/McpFallbackResult.cs`
- `tests/CfoAgent.Api.Tests/Mcp/McpFallbackTests.cs` only after its unique assertions are moved to `KnowledgeFileMcpAccessFallbackTests.cs`

**Implementation:**

1. Move fallback decision/logging into `KnowledgeFileMcpAccess`; preserve remote/local clients, development-only rule, path security, timeout, and caller cancellation.
2. Remove `McpFallbackResult<T>` because no production consumer uses its metadata.
3. Reduce `AgentRequest` to the message actually consumed by agents; keep public `ChatRequest.ConversationId` and response echo unchanged.
4. Remove `MaximumSpecialistInvocations` and its impossible postcondition; the explicit mixed branch itself bounds worker count.
5. Remove unused test `Clock` fields.
6. Add a Mock/fallback intent-classification parity test rather than introducing a classifier abstraction.
7. Retain overlapping historical phase-gate tests unless exact duplication and lossless consolidation are proven.

**Tests required:**

- all Knowledge fallback reasons still log and use local access only in Development when enabled;
- cancellation never triggers fallback;
- traversal, absolute path, symlink/junction, missing file, list, and read tests remain;
- public conversation ID behavior is unchanged;
- mixed flow still invokes exactly two fixed workers;
- Mock/fallback classifier parity covers every `CfoIntent`;
- no reflection/serialization test depends on removed internal request fields.

**Risk/rollback:** Fallback security has a higher risk than the small diff suggests. Move existing tests before deleting the coordinator. If any metadata consumer is found, retain `McpFallbackResult<T>` and document it instead of deleting it.

**Gate:** Knowledge security/fallback tests, agent/API tests, full backend tests, and Release tests.

### Step 6 - Final regression and documentation gate

**Goal:** Verify the target without further architectural changes.

**Expected documentation files to update only where facts changed:**

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- relevant setup/user documentation if invocation behavior is described
- a final task result document required by the later gate

**Validation required:**

- serialized Debug restore/build/test;
- serialized Release tests;
- frontend tests/build to prove public contract compatibility;
- `docker compose config`;
- existing container integration, outage recovery, and smoke tests;
- all five browser workflows;
- repository searches proving no API persistence, deleted symbols, stale package, or stale documentation claim remains;
- `git diff --check` and final status review.

**Risk/rollback:** Documentation must describe the implementation that actually passed, not the planned design. Any failed contract/container gate blocks completion and is fixed in the implementation step that caused it.

## File disposition summary

### New files

- `src/CfoAgent.Api/Agents/AgentResultComposer.cs`
- `src/CfoAgent.Api/Rag/Retrieval/IFinancialKnowledgeSearch.cs`
- `src/CfoAgent.Api/Rag/Chroma/ChromaFinancialKnowledgeSearch.cs` as the moved/renamed implementation
- `tests/CfoAgent.Api.Tests/Agents/AgentResultComposerTests.cs`
- `tests/CfoAgent.Api.Tests/AgentContractsTests.cs` as the moved/renamed contract test

### Moved/renamed files

- `Rag/Retrieval/FinancialKnowledgeRetrievalService.cs` -> `Rag/Chroma/ChromaFinancialKnowledgeSearch.cs`
- `tests/CfoAgent.Api.Tests/AgentContractsAndFrameworkTests.cs` -> `tests/CfoAgent.Api.Tests/AgentContractsTests.cs`

### Deleted files after replacement

- `src/CfoAgent.Api/Agents/Configuration/CfoAgentFramework.cs`
- `src/CfoAgent.Api/Mcp/KnowledgeFileMcpFallback.cs`
- `src/CfoAgent.Api/Mcp/McpFallbackResult.cs`
- `tests/CfoAgent.Api.Tests/Mcp/McpFallbackTests.cs` after unique coverage is moved

### Package removal

- `Microsoft.Agents.AI` from `src/CfoAgent.Api/CfoAgent.Api.csproj` after `CfoAgentFramework` removal.

No folder, MCP project, frontend file, Docker file, configuration key, public contract, or server contract is planned for deletion.

## Test strategy

Testing follows risk rather than folder count:

1. **Pure unit tests:** deterministic result composition, forecast calculations, classification fallback/parity, context bounding.
2. **Port contract tests:** finance typed client, vector-search port behavior, provider selection, cancellation, and typed exceptions.
3. **Adapter tests:** MCP initialization/discovery/cache/allow-list/call/reconnect, Chroma protocol mapping/filtering, knowledge local security.
4. **Application tests:** each specialist, single/mixed orchestrator call counts, metadata preservation, no LLM calculation.
5. **HTTP tests:** five scenarios, validation, correlation, model metadata, cancellation, sanitized 503/504/500 behavior.
6. **Ownership tests:** no API database provider or connection string, unchanged MCP/public contracts.
7. **Container tests:** real endpoints, capability discovery, PostgreSQL-backed finance, Chroma citations, outage/recovery, internal ports.
8. **Provider tests:** deterministic offline Mock always; opt-in live Ollama remains skipped by default.

Every implementation step runs its focused tests first, followed by:

```powershell
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

Release and Docker gates run at the steps explicitly touching provider/MCP behavior and at the final gate.

## Rollback principles

- Keep commits aligned to the steps above; do not combine agent composition, MCP security, vector transport, and fallback cleanup in one change.
- Add replacement tests before deleting behavior or files.
- Revert the smallest failing step, never public contracts or safety tests.
- Do not compensate for a failed refactor with a new framework, compatibility wrapper, service locator, or duplicate path.
- Preserve prior files when a deletion's lack of use cannot be proven.

## Explicit non-goals

- no changes to Finance MCP or Knowledge MCP server code/contracts;
- no changes to PostgreSQL ownership, migrations, or seed data;
- no ChromaDB replacement or embedding change;
- no frontend or public API redesign;
- no new agents, dynamic planner, autonomous loop, or arbitrary tool selection;
- no cloud model provider, authentication, streaming, or persistent history;
- no MediatR, CQRS, event bus, workflow engine, plugin registry, policy engine, repository, unit of work, agent base class, or custom schema engine;
- no new project or microservice;
- no broad namespace migration or unrelated cleanup.
