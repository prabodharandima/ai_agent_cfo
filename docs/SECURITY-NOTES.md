# Security Notes

## Prompt and source handling

- Prompts are treated as untrusted input. The orchestrator routes only to the five MVP finance and knowledge intents; it does not execute instructions embedded in prompts.
- The Mock LLM receives only bounded, verified finance results or bounded retrieved knowledge context. It is never used as a calculation authority.
- ChromaDB retrieval remains source-grounded. Responses include citations from indexed Markdown documents, and the knowledge agent returns an explicit insufficient-knowledge result rather than inventing an answer.
- Application logs record message length, routing intent, agent names, outcome, and dependency status. They do not record raw prompts, document contents, financial rows, SQL, or MCP configuration values.

## MCP controls

- Finance MCP exposes an allow-list of five read-only tools. Knowledge File MCP exposes only `list_knowledge_files` and `read_knowledge_file`.
- Knowledge file access is rooted under `data/knowledge`; absolute paths, traversal, links/junction escapes, and write or execution operations are rejected.
- MCP clients are disabled by default, start lazily, use timeouts and caller cancellation, validate required tools, and use the existing deterministic local fallback only when configured.

## HTTP safeguards

- Every request receives an `X-Correlation-ID`; callers may supply a bounded safe identifier for troubleshooting.
- Errors use Problem Details with a trace ID and sanitized titles. Exception details are not sent to clients.
- The chat endpoint has a small built-in fixed-window rate limit. Production deployment should add environment-specific edge rate limiting and monitoring rather than a custom resilience framework.
