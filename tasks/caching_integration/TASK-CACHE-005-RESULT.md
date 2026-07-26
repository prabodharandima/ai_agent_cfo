# TASK-CACHE-005 Result

## Earlier changes detected

- TASK-CACHE-001 is complete and committed as `d266185`.
- TASK-CACHE-002 is complete and committed as `a207bea`.
- TASK-CACHE-003 is complete and committed as `6386eff`.
- TASK-CACHE-004 is complete and committed as `c29b7e5`.
- The provider-neutral cache, optional Redis backing, and Finance, RAG, embedding, and MCP-discovery cache behavior were retained.

## Implementation

- `CfoOrchestratorAgent` now caches only successful, schema-validated, non-`Unsupported` LLM intent classifications through `IApplicationCache`.
- The existing deterministic intent fallback remains unchanged and is never cached.
- Malformed or oversized output, `Unsupported` model output, provider errors, prompt-risk blocks, timeouts, and caller cancellation are not cached.
- Cache infrastructure failure continues to fail open through `HybridApplicationCache` and performs the normal LLM classification.
- Session-dependent requests use a hash of the existing safe session metadata only: prior response types and optional deterministic date periods. Raw session prompts, answers, RAG content, MCP data, and conversation IDs are not cached.
- The classification key also fingerprints the configured prompt-risk policy so an existing distributed entry cannot bypass a newly changed block-list.

## Cache key and TTL

The key shape is:

`llm-classification:{providerSha256}:{modelSha256}:{promptVersionSha256}:{allowedIntentSetSha256}:{sessionContextFingerprint}:{normalizedMessageSha256}:{promptRiskPolicySha256}`

- `Cache:Classification:TtlSeconds` defaults to 300 seconds.
- `Cache:Classification:PromptVersion` defaults to `v1`; change it after changing the classifier prompt/template.
- `Cache:Classification:AllowedIntentSetVersion` defaults to `v1`; change it after changing the classification policy. The current `CfoIntent` enum set is also fingerprinted.
- Compose maps `CACHE_LLM_CLASSIFICATION_TTL_SECONDS`, `CACHE_LLM_CLASSIFICATION_PROMPT_VERSION`, and `CACHE_LLM_CLASSIFICATION_ALLOWED_INTENT_SET_VERSION`.

## Tests and validation

- Focused `CachedIntentClassificationTests`: 8 passed, 0 failed, 0 skipped.
- Solution restore and serialized Debug build: succeeded with 0 warnings and 0 errors.
- Debug full suite with Docker access: 267 passed, 7 failed, 8 skipped; 282 total.
- Release full suite with Docker access: 267 passed, 7 failed, 8 skipped; 282 total.
- `docker compose config --quiet`: passed after adding the non-secret cache defaults omitted from the existing local `.env` file.
- `git diff --check`: passed.

The seven full-suite failures are the same baseline agent-test inconsistencies recorded by Tasks 2 through 4: one obsolete structured-output marker expectation, three obsolete exception-type expectations, and three stale prompt or cancellation call-count expectations. They are unrelated to LLM classification caching.

## Documentation

Updated `AGENT.md`, `APPLICATION_ARCHITECTURE.md`, `IMPLEMENTATION-PLAN.md`, `README.md`, `.env.example`, and Docker Compose with classification-cache behavior, safe key content, TTL, versions, and graceful degradation.

## Completion

TASK-CACHE-005 is complete. No additional caching task exists in this task pack.
