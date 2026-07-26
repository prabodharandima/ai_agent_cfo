# TASK-CACHE-005 — Cache LLM Classification

## Goal
Cache only successful validated intent-classification results. Do not cache final LLM responses.

## Requirements
Use `IApplicationCache`. Key must include:
- normalized user-message hash;
- provider;
- model;
- classifier prompt/template version;
- allowed-intent-set version;
- safe session/context version when classification depends on memory.

If session context cannot be represented safely, skip caching for that request. Do not cache malformed output, deterministic fallback unless justified, injection-blocked requests, provider errors, timeouts, or cancellation. Cache failure must execute normal classification. Keep all current validation and fallback behavior.

## Focused tests
1. Identical stateless prompt reuses classification.
2. Provider/model/prompt-version changes key.
3. Malformed output is not cached.
4. Provider failure is not cached.
5. Cache failure falls back.
6. Cancellation is not cached.
7. Session-dependent classification is safe.

Run:
```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
docker compose config
git diff --check
```

Update final architecture/setup docs with cache abstraction, Redis/HybridCache, cached areas, TTLs, versioning, graceful degradation, security, and invalidation limitations. Create `TASK-CACHE-005-RESULT.md`.
