# Future Improvements

These are production discussion items only. They are not implemented in this MVP.

## Model and retrieval

- Add a vetted real-provider `IChatClient` adapter, such as Azure OpenAI, OpenAI, Ollama, or Claude, with configuration stored outside source control.
- Use a managed embedding model and evaluate retrieval quality against a versioned finance knowledge test set.
- Replace local ChromaDB with a managed vector service such as Azure AI Search or PostgreSQL with pgvector when operational requirements justify it.
- Add document lifecycle controls, metadata filtering, and ingestion observability while preserving citations.

## Data and forecasting

- Move from SQLite demo data to a managed relational database such as Azure SQL or PostgreSQL.
- Add governed data imports, reconciliation checks, reporting periods, and audit trails.
- Validate and version more sophisticated forecasting models, with deterministic fallback calculations and documented assumptions.

## Product and operations

- Add authentication, authorization, tenant boundaries, and durable chat history only after defining data-retention requirements.
- Add deployment-specific rate limits, metrics, tracing export, alerting, and secrets management.
- Package local dependencies for a repeatable development environment and establish CI validation for the serialized build/test commands.
- Consider streaming UI responses only after preserving structured result contracts, cancellation behavior, and safe error handling.

These improvements must preserve the core rule: an LLM can assist with intent and explanation, but verified finance calculations remain deterministic and source-grounded.
