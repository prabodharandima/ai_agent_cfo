# TASK-CACHE-001 — Cache Foundation, Redis, and Finance MCP Results

## Goal
Add a provider-neutral cache adapter using HybridCache with Redis, then cache every read-only operation on `IFinanceMcpClient`.

## Before editing
Read repository instructions, architecture, current task result files, `src/CfoAgent.Api`, related tests, Docker Compose, and configuration. Run `git status` and `git log --oneline -10`.

## Requirements
- Add a small `IApplicationCache` abstraction.
- Implement it with HybridCache.
- Configure Redis only in the composition root.
- Internal agents/clients must not reference Redis.
- Support disabling cache or memory-only local mode.
- Cache failure must fall back to the authoritative dependency.
- Do not cache exceptions, invalid results, timeouts, or cancellation.
- Use deterministic keys without raw prompts or secrets.
- Add Redis to Docker Compose on the internal network with health check.
- Redis must not be a mandatory source of truth.
- Add configurable TTLs for all Finance operations.

Implement a Decorator around `IFinanceMcpClient` and cache all current typed read-only operations: summaries, comparisons, top products, historical sales, budget target, and any other operation found.

## Focused tests
1. Miss calls inner client once.
2. Repeated identical request is cached.
3. Different arguments use different keys.
4. Disabled cache calls inner client.
5. Cache failure falls back.
6. Cancellation is not cached.

Use test doubles; no live Redis for unit tests.

Run focused tests, then:
```bash
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
docker compose config
git diff --check
```

Update only affected docs. Create `TASK-CACHE-001-RESULT.md`.
