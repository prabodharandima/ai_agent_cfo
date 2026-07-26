# TASK-CACHE-002 — Cache RAG Retrieval

## Goal
Cache ChromaDB retrieval results through a Decorator around `IFinancialKnowledgeSearch`.

## Requirements
Use `IApplicationCache`. Key must include:
- normalized question hash;
- topK;
- document type;
- period;
- retrieval threshold if relevant;
- RAG index version.

Do not store raw questions in keys. Add configurable TTL and index version. Cache successful retrieval results only. Preserve source metadata, citations, filtering, thresholds, and public contracts. Cache failure must call ChromaDB normally. Do not cache dependency failures or cancellation.

## Focused tests
1. Identical query calls inner search once.
2. Changed filters/topK create another key.
3. Changed index version bypasses old entry.
4. Cache failure falls back.
5. Cancellation is not cached.
6. Sources and citations survive caching.

Run focused tests, build, full tests, and `git diff --check`. Update affected docs and create `TASK-CACHE-002-RESULT.md`.
