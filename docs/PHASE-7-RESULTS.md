# Phase 7 Results: Optional Local Ollama Provider

## Status

Passed on 2026-07-18. Phase 7 adds an optional local Ollama chat provider while preserving the default deterministic Mock provider, the four in-process agents, deterministic finance calculations, ChromaDB retrieval, deterministic embeddings, and both MCP integrations.

## Implemented Scope

- `AiOptions` supports configuration-selected `Mock` and `Ollama` providers.
- `OllamaChatClient` is a thin `OllamaSharp`-backed `IChatClient` implementation. It applies configured model, temperature, context, output bounds, finite timeout, cancellation propagation, metadata, sanitized failures, and safe structured logging.
- The committed default remains `AI:Provider=Mock` with `AI:Model=DeterministicMock`.
- [appsettings.Ollama.example.json](../src/CfoAgent.Api/appsettings.Ollama.example.json) documents the local selection: `AI:Provider=Ollama`, `AI:Model=llama3.2:3b`, `AI:BaseUrl=http://localhost:11434`, and bounded generation settings.
- `OllamaHealthCheck` leaves liveness independent of Ollama. Readiness probes the lightweight tags endpoint only when Ollama is selected, with a finite timeout and model-presence validation.
- Provider failures are returned as sanitized Problem Details. Caller cancellation propagates and Ollama never silently falls back to Mock.
- Agent prompts preserve the existing Mock markers while adding provider-neutral guardrails. Classification has deterministic fallback when an Ollama response is malformed.
- Live Ollama tests are opt-in, sequential, and invoked with [test-live-ollama.ps1](../scripts/test-live-ollama.ps1). They never install or download a model.

## Authority And Boundaries

- Ollama formats bounded, verified content and may return a bounded intent token. It does not calculate or replace authoritative finance values.
- `SalesAnalysisService`, `SalesForecastingService`, SQLite, and approved Finance MCP tools remain responsible for finance calculations.
- ChromaDB remains the semantic retrieval and citation source. `DeterministicTokenHashEmbeddingGenerator` is unchanged.
- Finance MCP and Knowledge File MCP remain configuration-controlled, lazy, read-only, and unchanged. Ollama receives no MCP tools and cannot select tools or servers.

## Configuration And Operations

Use local configuration or environment variables, not committed secrets:

```powershell
$env:AI__Provider = "Ollama"
$env:AI__Model = "llama3.2:3b"
$env:AI__BaseUrl = "http://localhost:11434"
$env:AI__TimeoutSeconds = "120"
dotnet run --project src/CfoAgent.Api
```

To return to the default offline provider, clear the overrides or set `AI__Provider=Mock` and `AI__Model=DeterministicMock`.

Application startup is network-free. `/health/live` does not depend on Ollama. `/health/ready` checks Ollama only when selected and reports controlled unavailable, timeout, invalid-response, or missing-model status without prompts, RAG context, provider response bodies, sensitive URLs, or stack traces.

Local CPU latency varies with hardware, model load state, and concurrent work. No benchmark is claimed. Requests and tests use finite timeouts; cancellation is not converted into fallback.

## Validation

The serialized solution commands are required because of the local MSBuild project-reference race:

```powershell
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
```

Final Debug result: 159 total, 155 passed, 0 failed, 4 skipped. The skipped tests are the intentionally opt-in live Ollama category; the normal suite remains offline and does not contact Ollama.

Final Release result: 159 total, 155 passed, 0 failed, 4 skipped.

Focused offline Ollama coverage: 29 passed.

Local live validation was available:

```powershell
./scripts/test-live-ollama.ps1
```

With Ollama `0.32.0`, local `llama3.2:3b`, and indexed local Chroma knowledge, the sequential `LiveOllama` category passed 4 of 4 tests in 2 minutes 9 seconds. It covered endpoint/model availability, one bounded completion, a sales-summary API smoke flow, and a grounded knowledge flow with sources. If Ollama, the model, or the indexed Chroma collection is unavailable, the category reports an actionable skip; model download remains manual.

## Limitations, Deviations, And Blockers

- Ollama is a local optional dependency, not a cloud provider, production deployment feature, or automatic fallback path.
- Normal tests remain deterministic and offline; live text is intentionally not asserted as exact prose.
- No streaming, persistent chat history, new agents, provider registry, embedding migration, OpenAI integration, or Phase 8 work was added.
- Deviations: none.
- Blockers: none.

## Phase Gate

Phase 7 is complete. Mock remains the default provider, Ollama is configuration-controlled through the existing `IChatClient` boundary, all finance and RAG/MCP authority boundaries remain intact, offline Debug and Release gates pass, and the optional local live gate passes when the documented prerequisites are present.
