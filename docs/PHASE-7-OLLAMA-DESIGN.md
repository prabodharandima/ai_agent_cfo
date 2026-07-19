# Phase 7 Ollama Discovery and Design

> Historical design record: its SQLite/stdio deployment assumptions were superseded by Phase 8. The current container and host-Ollama behavior is documented in [PHASE-8-RESULTS.md](PHASE-8-RESULTS.md).

## 1. Status and scope

This document is the output of `TASK-CFO-P7-001`. It describes the current repository and proposes the smallest compatible design for an optional local Ollama provider using `llama3.2:3b`.

Nothing in this document is implemented by this task. Runtime code, configuration, tests, project files, package references, the React application, MCP integrations, RAG, embeddings, and financial calculations remain unchanged.

The proposal adds one future provider choice:

- `Mock`: current default, deterministic, offline, and retained.
- `Ollama`: future optional local provider using `llama3.2:3b`.

No other model provider is in scope.

## 2. Prerequisite and baseline discovery

### Repository prerequisites

- Phase 6 is complete according to `docs/FINAL-VALIDATION.md`.
- The latest recorded gate has 118 backend tests, 10 frontend unit tests, and 7 Playwright tests passing.
- `CfoAgent.sln` contains the expected four .NET projects:
  - `src/CfoAgent.Api/CfoAgent.Api.csproj`
  - `tests/CfoAgent.Api.Tests/CfoAgent.Api.Tests.csproj`
  - `tools/CfoAgent.FinanceMcpServer/CfoAgent.FinanceMcpServer.csproj`
  - `tools/CfoAgent.KnowledgeFileMcpServer/CfoAgent.KnowledgeFileMcpServer.csproj`
- Phase 7 identifies this task as its first task, so there is no earlier Phase 7 prerequisite.
- The worktree was clean at the start of discovery.

### Local environment observed during discovery

- .NET SDK: `10.0.302`, matching `global.json`.
- Ollama CLI: `0.32.0`.
- `llama3.2:3b` is already present in the local Ollama model list.
- `OllamaSharp` is not currently referenced by the solution or present in the local NuGet cache.

The installed Ollama runtime and model are useful for the later opt-in live-test task, but they are not prerequisites for normal restore, build, startup in Mock mode, or offline tests.

## 3. Current implementation

### 3.1 Current `IChatClient` registration

`src/CfoAgent.Api/Program.cs` currently:

1. binds `AI` to `CfoAgent.Api.Configuration.AiOptions`;
2. requires `AI:Provider` to equal `Mock`;
3. validates a nonblank `AI:Model` and nonnegative `AI:SimulatedDelayMilliseconds`;
4. registers `MockChatClient` as the only application `IChatClient` with:

```csharp
builder.Services.AddSingleton<IChatClient, MockChatClient>();
```

`CfoAgentFramework` is also a singleton. It receives that single selected `IChatClient` through constructor injection and passes it to each `ChatClientAgent` with `UseProvidedChatClientAsIs = true`.

### 3.2 Current Mock implementation

`src/CfoAgent.Api/AI/Mock/MockChatClient.cs` implements `Microsoft.Extensions.AI.IChatClient` directly. It provides:

- provider metadata `Mock`;
- configured model metadata, currently `DeterministicMock`;
- nonstreaming and interface-required streaming methods;
- configurable cancellable delay;
- configurable simulated failure;
- deterministic keyword classification;
- deterministic formatting of verified context; and
- no network calls.

Its protocol recognizes these current markers:

- `[MOCK:CLASSIFY]`
- `[MOCK:SALES_SUMMARY]`
- `[MOCK:SALES_COMPARISON]`
- `[MOCK:TOP_PRODUCTS]`
- `[MOCK:FORECAST]`
- `[MOCK:KNOWLEDGE]`
- `[MOCK:ORCHESTRATE]`

These marker names are a current implementation detail. They are used by application classes as well as direct Mock tests, so later tasks must preserve their Mock behavior even if the surrounding instructions are made provider-compatible.

### 3.3 Agent consumers

The four agents remain in the `CfoAgent.Api` process:

| Agent | Current model calls | Authoritative non-model work |
|---|---|---|
| `CfoOrchestratorAgent` | intent classification and final composition | explicit routing, maximum two specialists, result aggregation |
| `SalesAnalysisAgent` | executive wording | sales DTOs from Finance MCP or `SalesAnalysisService` |
| `ForecastingAgent` | forecast wording | historical data from Finance MCP/local service and regression in `SalesForecastingService` |
| `FinancialKnowledgeAgent` | wording from bounded retrieved context | Knowledge File MCP validation plus ChromaDB retrieval, distance filtering, and citations |

All model calls pass through `CfoAgentFramework.CreateAgent`. No agent resolves or constructs a provider directly.

### 3.4 Current prompt and formatting paths

- `AgentDefinitions` supplies agent names, descriptions, and system guardrails.
- `AgentPromptTemplates` serializes verified sales and forecast DTOs and prepends the current Mock markers.
- `FinancialKnowledgeAgent.BuildBoundedContext` limits retrieved context with `Rag:MaxKnowledgeContextCharacters`, currently 4,000 characters.
- `CfoOrchestratorAgent.ClassifyAsync` sends `[MOCK:CLASSIFY]` and parses the response as `CfoIntent`.
- `CfoOrchestratorAgent.ComposeAsync` sends only `OrchestratedSpecialistResult` values containing agent name, response type, and already verified structured data.
- Specialist answers are presentation text. Authoritative API data remains the original `StructuredData`, sources, assumptions, warnings, and data period.

The current classification is deterministic because the selected provider is `MockChatClient`. Merely replacing the provider without adapting the instructions would make this fragile: a real model is not guaranteed to understand the Mock marker or return exactly one `CfoIntent` name.

### 3.5 Current configuration and metadata

`src/CfoAgent.Api/appsettings.json` currently has:

```json
{
  "AI": {
    "Provider": "Mock",
    "Model": "DeterministicMock"
  }
}
```

`src/CfoAgent.Api/appsettings.Development.json` adds `SimulatedDelayMilliseconds = 0` and `SimulateFailure = false`.

`ChatEndpoints` creates the public response `model` object from `AiOptions.Provider` and `AiOptions.Model`. The frontend contract is already provider-neutral and does not need an Ollama-specific response type.

### 3.6 Current health, errors, cancellation, and logging

- `/health/live` has no dependencies.
- `/health/ready` currently checks SQLite, ChromaDB, and MCP configuration.
- `ApiExceptionHandler` maps dependency, timeout, cancellation-without-caller-cancellation, invalid-operation, and unexpected failures to sanitized Problem Details.
- `ChatEndpoints` catches a controlled orchestrator `InvalidOperationException` and returns a sanitized 503 response.
- The endpoint starts caller cancellation with `HttpContext.RequestAborted`; agents and the framework propagate it.
- Existing logging records operation metadata, intent, duration, outcome, and failure type without raw prompts or retrieved context.

There is currently no Ollama health check, provider exception, timeout, or log category.

### 3.7 Current test seams

The repository already has the seams needed for offline provider tests:

- `CfoAgentFramework` accepts any `IChatClient` through its constructor.
- Unit tests instantiate `MockChatClient` directly.
- Agent tests construct `CfoAgentFramework` around a chosen client.
- `ChatApiFactory` uses `WebApplicationFactory<Program>`, configuration overrides, and `ConfigureTestServices`.
- Chroma tests already use custom `HttpMessageHandler` implementations for deterministic HTTP behavior.
- MCP tests use focused stub interfaces and real opt-in process boundaries where appropriate.

No new generic provider registry, mediator, service locator, or test framework is needed.

## 4. Package and API decision

### Decision

Use the stable `OllamaSharp` package and its `OllamaSharp.OllamaApiClient`, which implements `Microsoft.Extensions.AI.IChatClient`. Wrap it only with one small application adapter where needed for timeout classification, sanitized provider failures, metadata, and safe logging.

As of this discovery on 2026-07-18, the recommended package version is `OllamaSharp` `5.4.27` for the later implementation task. The NuGet metadata lists a `net10.0` target and dependencies on `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.Abstractions` `>= 10.8.0`, matching the `10.8.0` abstraction already referenced by `CfoAgent.Api`.

### Why this option

- Microsoft documents `OllamaSharp.OllamaApiClient` as an `IChatClient` implementation in its current [.NET `IChatClient` guidance](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient).
- The [`OllamaSharp` NuGet package](https://www.nuget.org/packages/OllamaSharp) currently aligns with this repository's .NET and `Microsoft.Extensions.AI` versions.
- `OllamaApiClient` can be created with a configured `HttpClient`, preserving the repository's existing fake-HTTP-handler testing style.
- It avoids maintaining request/response mappings for the native Ollama API.
- It keeps Ollama transport types out of agents because the rest of the application continues to depend on `IChatClient`.

### Rejected options

| Option | Decision | Reason |
|---|---|---|
| `Microsoft.Extensions.AI.Ollama` | Reject | The [NuGet package is deprecated](https://www.nuget.org/packages/Microsoft.Extensions.AI.Ollama/), prerelease-only, and explicitly recommends `OllamaSharp`. |
| Small raw HTTP adapter over `/api/chat` | Reject as the primary design | The [native Ollama chat API](https://docs.ollama.com/api/chat) is straightforward, but hand-maintaining DTO mapping and `IChatClient` conversion duplicates a compatible maintained package without a repository-specific benefit. |
| Direct `OllamaApiClient` registration with no application wrapper | Insufficient by itself | It supplies `IChatClient`, but later tasks still require stable provider exceptions, timeout distinction, safe logging, and controlled malformed-response behavior. One thin wrapper is the smallest focused place for those policies. |

The package version must be rechecked during `TASK-CFO-P7-002` before the package reference is added. No package is added by this task.

## 5. Proposed configuration design

Extend the existing `AiOptions` rather than adding a parallel provider hierarchy. Keep `Provider` and `Model` as the selected-provider keys, and add flat Ollama transport/generation settings under the existing `AI` section.

Proposed default shape:

```json
{
  "AI": {
    "Provider": "Mock",
    "Model": "DeterministicMock",
    "BaseUrl": "http://localhost:11434",
    "TimeoutSeconds": 120,
    "Temperature": 0,
    "ContextLength": 4096,
    "MaxOutputTokens": 512,
    "SimulatedDelayMilliseconds": 0,
    "SimulateFailure": false
  }
}
```

`Mock` remains the committed default. A local developer selects Ollama through configuration overrides, for example:

```powershell
$env:AI__Provider = 'Ollama'
$env:AI__Model = 'llama3.2:3b'
$env:AI__BaseUrl = 'http://localhost:11434'
dotnet run --project src/CfoAgent.Api
```

Proposed validation:

| Key | Rule |
|---|---|
| `AI:Provider` | exactly one supported value, `Mock` or `Ollama`, compared case-insensitively and reported canonically |
| `AI:Model` | nonblank for both providers; `DeterministicMock` remains default and Ollama example is `llama3.2:3b` |
| `AI:BaseUrl` | required for Ollama; absolute `http` or `https`; no startup network call |
| `AI:TimeoutSeconds` | finite positive value; proposed allowed range 1 through 600, default 120 |
| `AI:Temperature` | finite value from 0 through 2, default 0 |
| `AI:ContextLength` | proposed safe application range 1,024 through 32,768, default 4,096 |
| `AI:MaxOutputTokens` | proposed range 1 through 1,024 and less than context length, default 512 |
| Mock simulation keys | retain current nonnegative delay and failure switch behavior |

Temperature zero reduces variation but does not make a real model mathematically deterministic. Offline tests must therefore assert contracts and guardrails, not exact live prose.

The model tag and bounds are configuration, not source constants. Startup validates values but does not contact Ollama, list models, pull models, or load a model.

## 6. Proposed runtime component design for later tasks

The following files are proposed for later Phase 7 tasks; they are not created now:

| Proposed change | Purpose |
|---|---|
| extend `src/CfoAgent.Api/Configuration/AiOptions.cs` | selected provider plus Ollama base URL, timeout, temperature, context, and output bounds |
| add `src/CfoAgent.Api/AI/Ollama/OllamaChatClient.cs` | thin `IChatClient` wrapper around `OllamaApiClient` |
| add focused provider exception types under `src/CfoAgent.Api/AI/Ollama/` | stable unavailable, timeout, and malformed-response categories |
| conditionally update `src/CfoAgent.Api/Program.cs` | validate options, configure one HTTP client, and register exactly one selected `IChatClient` |
| update `AgentDefinitions`, `AgentPromptTemplates`, and `CfoOrchestratorAgent` only where required | concise provider-compatible instructions and safe classification parsing |
| add `src/CfoAgent.Api/Health/OllamaHealthCheck.cs` | conditional readiness without affecting liveness or startup |
| extend existing tests | provider selection, fake transport, agents, health, API errors, and Mock regression |

No additional project, agent, MCP server, provider registry, fallback framework, or model-driven tool layer is proposed.

### Provider registration

`Program.cs` should choose the provider once from validated `AiOptions` and register exactly one service as `IChatClient`:

```text
AI:Provider = Mock
    -> IChatClient = MockChatClient

AI:Provider = Ollama
    -> configured HttpClient
    -> OllamaApiClient
    -> thin application OllamaChatClient
    -> IChatClient
```

Registration must be lazy with respect to network activity. Constructing services may create managed client objects, but it must not call `/api/chat`, `/api/tags`, pull the model, launch Ollama, or preload `llama3.2:3b`.

`CfoAgentFramework` and every agent continue to receive only `IChatClient`; no agent branches on provider name.

### Ollama request behavior

The future adapter should:

1. map Agent Framework messages and instructions through `OllamaApiClient`;
2. use the configured model `llama3.2:3b`;
3. apply configured temperature, context length, and output bound using the compatible OllamaSharp request options available in the selected version;
4. propagate the caller `CancellationToken` unchanged;
5. rely on a finite configured `HttpClient.Timeout` or an equivalent linked timeout;
6. distinguish caller cancellation from provider timeout;
7. reject an empty or unusable completion as a malformed provider response;
8. expose `ChatClientMetadata` with provider `Ollama`, configured endpoint, and configured model; and
9. implement the interface-required streaming method without adding streaming to `POST /api/chat`.

No automatic retries are proposed. A local cold model load can be slow, so the request timeout is deliberately finite but larger than dependency health timeout. No latency number is claimed or guaranteed.

## 7. Provider-compatible agent behavior

### Classification

The current `[MOCK:CLASSIFY]` request relies on Mock-specific parsing. The smallest compatible evolution is:

- preserve the current marker so existing Mock behavior and direct tests remain valid;
- strengthen `AgentDefinitions.CfoOrchestrator.SystemInstructions` with a concise classification protocol for the seven existing `CfoIntent` values;
- request exactly one intent name with no prose;
- trim and length-bound the returned text before `Enum.TryParse`;
- use the current deterministic keyword rules as the safe fallback for empty, malformed, or unknown output; and
- never route to more than the existing two specialists.

Mock mode must retain its exact deterministic classification. Ollama classification with temperature zero is still model output, so the safe parser and deterministic fallback are required.

### Specialist formatting

`AgentPromptTemplates` should continue to send only verified DTO JSON or bounded RAG context. Agent system instructions should tell the 3B model to:

- produce concise executive wording;
- use only supplied values;
- not calculate, alter, rank, infer, or add financial figures;
- state when supplied data is insufficient; and
- avoid returning tool calls or instructions.

The application must never parse revenue, profit, percentages, dates, rankings, budget values, or forecast values back from Ollama text. `AgentResult.StructuredData` remains the original service/MCP result.

An empty or unusable formatting response becomes a controlled provider error. It must not silently switch to Mock. The existing deterministic Mock remains selectable explicitly through configuration.

### Orchestration

`CfoOrchestratorAgent.ComposeAsync` continues to supply only `OrchestratedSpecialistResult` values. It does not give Ollama direct services, MCP clients, tools, SQL, paths, or agent-selection control beyond the bounded classification response.

### RAG

`FinancialKnowledgeAgent` continues to:

1. validate/list the approved file boundary through the existing Knowledge File MCP path;
2. retrieve semantic matches through `FinancialKnowledgeRetrievalService` and ChromaDB;
3. enforce TopK, distance filtering, deduplication, and the 4,000-character context bound;
4. pass only surviving context to the selected `IChatClient`; and
5. return citations from retrieval metadata, not from model-generated text.

Ollama does not replace ChromaDB and does not replace `DeterministicTokenHashEmbeddingGenerator`.

## 8. Unchanged finance and MCP authority

The following boundaries are binding and unchanged:

- SQLite remains authoritative for structured finance data.
- Finance calculations remain in `SalesAnalysisService`, `SalesForecastingService`, and equivalent Finance MCP server C#/EF queries.
- Forecast regression and scenarios remain deterministic C#.
- Sales and Forecasting agents continue to choose approved Finance MCP operations through existing configuration and fallback classes.
- `FinancialKnowledgeAgent` continues to use the existing Knowledge File MCP facade and ChromaDB retrieval.
- Ollama receives no MCP tools and cannot choose, invoke, or construct MCP calls.
- Both MCP integrations remain disabled by default, lazy, read-only, cancellable, timed, and locally fallible according to their existing policies.
- Ollama failure does not alter MCP fallback behavior.

## 9. Failure, timeout, health, and logging design

### Provider failures

Use small provider-specific exceptions or one exception with a stable category:

| Failure | Behavior |
|---|---|
| caller cancellation | rethrow `OperationCanceledException`; never convert to timeout or fallback |
| configured request timeout | controlled timeout category; sanitized 504 Problem Details |
| refused connection, DNS/transport failure, non-success response | unavailable category; sanitized 503 Problem Details |
| empty or malformed provider response | malformed category; sanitized 503 Problem Details |
| unsupported/invalid configuration | fail startup validation before serving requests |

Agent and orchestrator catch blocks must preserve these known provider categories instead of hiding timeout identity inside generic `InvalidOperationException` wrappers. Unexpected failures remain sanitized by `ApiExceptionHandler`.

There is no automatic fallback from Ollama to Mock. Provider selection is explicit configuration. This prevents a real-provider outage from silently changing response semantics.

### Readiness

- `/health/live` remains dependency-free.
- In Mock mode, Ollama readiness returns healthy/not selected without making any Ollama request.
- In Ollama mode, readiness uses a short finite timeout and a lightweight request such as the official [`GET /api/tags`](https://docs.ollama.com/api/tags) endpoint.
- Readiness confirms the endpoint responds and the configured `llama3.2:3b` tag is available.
- Readiness does not submit a CFO prompt, generate text, pull a model, or preload it.
- Ollama readiness is added alongside, not instead of, existing SQLite, ChromaDB, and MCP readiness checks.

### Logging

Safe structured fields:

- provider (`Mock` or `Ollama`);
- configured model;
- operation (`chat`, `classification`, `readiness`);
- elapsed milliseconds;
- outcome; and
- stable failure category.

Never log:

- raw prompts;
- verified DTO JSON;
- RAG context or document content;
- response bodies;
- full endpoint URLs or sensitive path/query values;
- stack traces or exception messages in normal controlled-failure logs; or
- model-management commands.

## 10. Test design

### Normal offline suite

Normal `dotnet test` remains fully offline and must not require the Ollama process or model. Add focused tests in the existing xUnit project for:

- Mock and Ollama provider selection;
- unsupported provider rejection;
- Ollama option validation;
- exactly one selected `IChatClient`;
- no startup HTTP request;
- request model and bounded generation options;
- response text and `ChatClientMetadata` mapping;
- caller cancellation;
- configured timeout;
- unavailable, non-success, empty, and malformed responses;
- sanitized Problem Details;
- Mock readiness making no Ollama request;
- Ollama readiness available/unavailable/model-missing responses;
- all five MVP routes through an Ollama-style fake `IChatClient`;
- malformed classification safe fallback;
- unchanged structured financial values;
- preserved RAG citations;
- unchanged MCP behavior; and
- complete Mock regression.

Use existing seams:

- custom `HttpMessageHandler` for transport tests;
- direct `IChatClient` injection into `CfoAgentFramework` for agent tests;
- `WebApplicationFactory<Program>` and `ConfigureTestServices` for API/readiness tests.

Do not make offline tests assert exact prose from a real model.

### Opt-in live suite

Later `TASK-CFO-P7-007` should gate all real requests behind an explicit variable such as:

```text
CFO_AGENT_RUN_OLLAMA_TESTS=true
```

The live suite should:

- read endpoint and model from configuration;
- require, but never install or pull, `llama3.2:3b`;
- run sequentially;
- use short prompts and bounded output;
- use generous but finite timeouts;
- verify one basic completion and one sales flow;
- optionally verify one grounded knowledge flow when local Chroma data is ready; and
- report an actionable skip/failure when Ollama or the model is unavailable.

The default solution test command must never opt in implicitly.

## 11. Proposed Phase 7 implementation sequence

| Task | Design allocation |
|---|---|
| `TASK-CFO-P7-002` | add the verified package, extend `AiOptions`, validate `Mock`/`Ollama`, and select exactly one provider without network startup |
| `TASK-CFO-P7-003` | add the thin OllamaSharp-backed client, request mapping, metadata, timeout/cancellation, fake transport, and sanitized provider failures |
| `TASK-CFO-P7-004` | make existing prompts/instructions provider-compatible, add safe classification parsing/fallback, and preserve finance/RAG/MCP guardrails |
| `TASK-CFO-P7-005` | conditional readiness, Problem Details mapping, cancellation preservation, and safe structured logs |
| `TASK-CFO-P7-006` | complete the deterministic offline gate |
| `TASK-CFO-P7-007` | add explicitly opt-in, sequential live Ollama tests |
| `TASK-CFO-P7-008` | update user-facing documentation and record the final Phase 7 gate |

Each task must stop at its own scope. In particular, `TASK-CFO-P7-002` must not probe Ollama and `TASK-CFO-P7-003` must not begin live tests.

## 12. Assumptions, risks, and blockers

### Assumptions

- `llama3.2:3b` is the required Phase 7 model tag.
- Local Ollama uses the default base URL `http://localhost:11434` unless configuration overrides it.
- A 4,096-token application context and 512-token output are conservative defaults for this bounded CFO workflow; validation allows controlled changes.
- A 120-second request timeout is a finite initial default intended to tolerate local cold starts, not a latency promise.
- The current 4,000-character RAG context limit and 4,000-character HTTP prompt limit remain unchanged.

### Risks to verify during implementation

- Confirm the exact OllamaSharp `5.4.27` option mapping for context length and output tokens when `TASK-CFO-P7-003` compiles against the package.
- A small 3B model can return extra prose or malformed classification despite temperature zero; strict parsing and deterministic fallback are mandatory.
- Live response latency varies by CPU, memory, model load state, and Ollama configuration; tests must use finite but non-brittle timeouts.
- Existing `[MOCK:*]` tags are coupled to direct Mock tests. Later prompt changes must preserve those tests or retain compatibility aliases.

### Blockers

No blocker exists for this design task. The package is not cached locally, so the later package-addition task will require ordinary NuGet restore access. Ollama and `llama3.2:3b` are already available locally for the later opt-in validation, but normal implementation and tests must not depend on that fact.

## 13. Acceptance-criteria mapping

| Acceptance criterion | Design status |
|---|---|
| References actual repository classes and paths | Complete: sections 3, 6, and 10 identify current classes, registrations, files, and test seams. |
| No runtime file changed | Complete for this task: this document is the only intended change. |
| Mock preserved | Complete: Mock remains default, its markers/tests remain compatible, and there is no automatic provider fallback. |
| MCP preserved | Complete: both existing MCP integrations and their policies remain unchanged. |
| ChromaDB and current embeddings preserved | Complete: Ollama is chat-only; semantic retrieval and deterministic embeddings remain unchanged. |
| Deterministic calculations preserved | Complete: no finance value moves to model output. |
| No speculative provider beyond Ollama | Complete: only Mock and Ollama are designed. |
