# TASK-CACHE-003 — Cache Embeddings

## Goal
Cache embeddings through a Decorator around the existing embedding abstraction without referencing Redis directly.

## Requirements
Use `IApplicationCache`. Key must include:
- normalized text hash;
- generator/provider identity;
- vector dimension;
- embedding version.

Preserve batch behavior and output order. Where practical, cache each text independently for partial batch hits. Do not store raw text in keys. Do not change the embedding algorithm or dimension. Cache failure must run the inner generator. Do not cache exceptions or cancellation.

## Focused tests
1. Identical text reuses cached vector.
2. Mixed hit/miss batch preserves order.
3. Different text/version creates another key.
4. Cache failure falls back.
5. Cancellation is not cached.
6. Vector values remain identical.

Run focused tests, build, full tests, and `git diff --check`. Update affected docs and create `TASK-CACHE-003-RESULT.md`.
