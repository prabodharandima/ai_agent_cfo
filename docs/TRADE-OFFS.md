# Architecture Trade-Offs

## Single ASP.NET Core monolith

The API contains the agents, orchestration, HTTP endpoint, deterministic finance services, RAG integration, configuration, and fallback decisions. This keeps the MVP easy to run, debug, and test as one deployment unit. The two local MCP processes are narrow tool adapters; they do not own business workflows, data, or independent deployment contracts, so they are not treated as microservices.

## Deterministic finance calculations

All financial summaries, comparisons, top products, and forecasts are calculated by SQL/C#. This makes values reproducible and auditable for the fixed demo data. The trade-off is a deliberately limited forecasting model rather than an adaptive statistical model.

## Mock LLM behind IChatClient

`MockChatClient` is the only registered model provider. It makes intent handling and explanations deterministic and removes credential/network risk from the MVP. The `IChatClient` boundary is retained so a real provider can be added later without letting it become a financial calculation engine.

## ChromaDB for knowledge retrieval

Markdown knowledge documents are embedded locally and retrieved through ChromaDB. This produces source-grounded responses and citations. The trade-off is a local development dependency and deterministic embeddings rather than a production-grade embedding model.

## Optional local MCP processes

Finance MCP exposes five read-only finance tools; Knowledge File MCP exposes only list/read operations rooted under `data/knowledge`. Both are disabled by default, lazy, timeout-bounded, cancellable, and capability-checked. Local deterministic and direct document paths remain explicit fallbacks. This adds integration coverage without making the MVP dependent on a process at startup.

## Deliberate omissions

Authentication, persistent chat history, streaming, autonomous loops, background jobs, CQRS/MediatR, microservices, and real LLM credentials are intentionally outside the MVP. They would add operational and security surface without improving the five required demo scenarios.
