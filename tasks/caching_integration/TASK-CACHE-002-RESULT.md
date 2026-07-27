# TASK-CACHE-002 Result

## Earlier changes detected

- TASK-CACHE-001 is complete and committed as `d266185`.
- The provider-neutral `IApplicationCache`, HybridCache implementation, optional Redis composition, and Finance MCP cache decorator were retained unchanged.

## Implementation

- Added `CachedFinancialKnowledgeSearch` as the scoped `IFinancialKnowledgeSearch` decorator.
- Kept `ChromaFinancialKnowledgeSearch` as the authoritative ChromaDB implementation.
- Registered the decorator in `Program.cs`; agents and business code remain independent of Redis and HybridCache.
- Added `Cache:Rag:RetrievalTtlSeconds`, defaulting to 300 seconds.
- Added `Rag:IndexVersion`, defaulting to `v1`, with startup validation.
- Cache infrastructure failure continues to fail open through `HybridApplicationCache`; ChromaDB failures and caller cancellation are rethrown and are not cached.
- Source metadata, distances, warnings, and citations remain in the cached retrieval result.

## Cache keys and TTL

The key shape is:

`rag:v1:retrieval:{indexVersion}:{normalizedQuestionSha256}:{topK}:{documentTypeHashOrNone}:{periodHashOrNone}:{maximumRetrievalDistance}`

- Raw questions, retrieved document text, secrets, and final chat answers are not stored in keys.
- Question and optional filter values are normalized before SHA-256 hashing.
- The configured `Rag:IndexVersion` is included directly. Change it after rebuilding or replacing the ChromaDB index to bypass prior retrieval entries without flushing Redis.
- The default retrieval TTL is 300 seconds and is configurable with `Cache:Rag:RetrievalTtlSeconds` or `CACHE_RAG_RETRIEVAL_TTL_SECONDS`.

## Tests and validation

- Focused retrieval tests: 15 passed, 0 failed, 0 skipped.
- Solution build: succeeded with 0 warnings and 0 errors.
- Full suite with Docker access: 251 passed, 7 failed, 8 skipped; 266 total.
- `git diff --check`: passed. Only line-ending conversion notices were reported.

The seven full-suite failures are pre-existing Sales/agent test inconsistencies unrelated to retrieval caching:

- one test expects the obsolete `STRUCTURED_SALES_PERIOD_OUTPUT` prompt marker;
- three tests expect `InvalidOperationException`, while current production code intentionally returns `AiProviderException` for invalid structured model output;
- three tests expect older sales-summary prompt/cancellation call counts.

During validation, `TestChatClient.cs` also contained a committed duplicate `System.Text.Json` import and a call to a missing `ResolveSalesSummaryDateRange` helper. The minimal helper was restored from the earlier committed implementation so the test project could compile. No production behavior changed.

## Documentation

Updated `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, `IMPLEMENTATION-PLAN.md`, and `README.md` with RAG retrieval caching, safe key contents, TTL, and index-version invalidation behavior.

## Completion

TASK-CACHE-002 implementation is complete. Its final full-suite validation gate remains blocked by the seven unrelated baseline test failures listed above. Later caching tasks were not started.
