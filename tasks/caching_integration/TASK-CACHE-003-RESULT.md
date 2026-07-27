# TASK-CACHE-003 Result

## Earlier changes detected

- TASK-CACHE-001 is complete and committed as `d266185`.
- TASK-CACHE-002 is complete and committed as `a207bea`.
- The provider-neutral `IApplicationCache`, optional Redis backing, Finance MCP cache decorator, and RAG retrieval cache decorator were retained unchanged.

## Implementation

- Added `CachedEmbeddingGenerator`, an `IEmbeddingGenerator<string, Embedding<float>>` decorator.
- Registered the existing `DeterministicTokenHashEmbeddingGenerator` as the authoritative singleton implementation and wrapped it at the composition root.
- Both RAG ingestion and ChromaDB query embedding generation now use the decorator without changing their callers, embedding algorithm, or 256-dimensional vectors.
- Each input is cached independently, preserving input batch order and allowing partial batch hits.
- Cached arrays are copied before being returned so one caller cannot mutate a later cached vector.
- Cache infrastructure failures continue to fail open through `HybridApplicationCache`; generator failures and caller cancellation propagate and are not cached.

## Cache keys and TTL

The key shape is:

`embedding:v1:{generatorIdentitySha256}:{dimension}:{embeddingVersionSha256}:{normalizedTextSha256}`

- Source text is never included in a key.
- The current deterministic generator identity and its 256-vector dimension are included.
- `Cache:Embeddings:Version` defaults to `v1`; change it when the embedding behavior or dimension changes to bypass prior cached vectors without flushing Redis.
- `Cache:Embeddings:TtlSeconds` defaults to 3,600 seconds and is configurable through `CACHE_EMBEDDINGS_TTL_SECONDS`.
- `CACHE_EMBEDDINGS_VERSION` maps to `Cache:Embeddings:Version` in Compose.

## Tests and validation

- Focused embedding cache tests: 5 passed, 0 failed, 0 skipped.
- Broader embedding and RAG tests: 22 passed, 0 failed, 0 skipped.
- Solution restore and build: succeeded with 0 warnings and 0 errors.
- Full suite with Docker access: 256 passed, 7 failed, 8 skipped; 271 total.
- `git diff --check`: passed.

The seven full-suite failures are the existing baseline agent-test inconsistencies recorded by TASK-CACHE-002: one obsolete structured-output marker expectation, three obsolete exception-type expectations, and three stale prompt or cancellation call-count expectations. They are unrelated to embedding caching. The initial sandboxed full-suite attempt additionally could not access Docker Desktop's named pipe; the Docker-enabled rerun executed those tests successfully and left only the same seven baseline failures.

## Documentation

Updated `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, `IMPLEMENTATION-PLAN.md`, and `README.md` with the embedding cache boundary, safe key content, TTL, and version invalidation behavior.

## Completion

TASK-CACHE-003 is complete. Later caching tasks were not started.
