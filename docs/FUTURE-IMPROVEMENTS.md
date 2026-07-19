# Future Improvements

These are production discussion items only. They are not implemented in this application.

## Model And Retrieval

- Add provider-quality evaluation, response-time budgets, and an explicit test policy for optional Ollama usage while retaining Mock as the offline deterministic default.
- Evaluate managed embedding and vector-store options only when operational requirements justify replacing the current deterministic embedding and ChromaDB deployment.
- Add document lifecycle controls, metadata filtering, and ingestion observability while preserving citations.

## Data And Forecasting

- Add governed Finance MCP data imports, reconciliation checks, reporting periods, and audit trails.
- Validate and version more sophisticated deterministic forecasting models with documented assumptions.

## Product And Operations

- Add authentication, authorization, tenant boundaries, and durable chat history only after defining data-retention requirements.
- Add deployment-specific rate limits, metrics, tracing export, alerting, secrets management, and CI validation for serialized build/test commands.
- Consider streaming UI responses only after preserving structured result contracts, cancellation behavior, and safe error handling.

These improvements must preserve the core rule: an LLM can assist with intent and explanation, but verified finance calculations remain deterministic and source-grounded.
