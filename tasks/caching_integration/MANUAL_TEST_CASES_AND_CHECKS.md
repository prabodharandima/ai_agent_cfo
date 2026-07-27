# Manual Cache Checks And Test Cases

This guide verifies the five application cache layers in the running Docker deployment:

| Layer | Key prefix or marker | What is cached | Default TTL |
| --- | --- | --- | --- |
| Finance MCP reads | `finance:v1` | Typed finance read results | 60 to 3,600 seconds by operation |
| RAG retrieval | `rag:v1:retrieval` | ChromaDB retrieval result and citations | 300 seconds |
| Embeddings | `embedding:v1` | Individual deterministic 256-value vectors | 3,600 seconds |
| MCP tool discovery | `mcp-discovery` | Approved MCP tool names only | 300 seconds |
| LLM classification | `llm-classification` | Validated non-`Unsupported` intent only | 300 seconds |

The cache is an optimization, never the source of truth. PostgreSQL, ChromaDB, Finance MCP, Knowledge MCP, and Ollama remain authoritative for their respective work.

## Prerequisites

1. Docker Desktop is running.
2. Ollama is running on the Windows host and has the model named by `OLLAMA_MODEL`.
3. The repository `.env` contains the cache settings from `.env.example`, including the `CACHE_EMBEDDINGS_*`, `CACHE_MCP_DISCOVERY_*`, and `CACHE_LLM_CLASSIFICATION_*` values.
4. Start or rebuild the deployment:

```powershell
docker compose up --build -d
```

5. Confirm the API, Redis, both MCP services, ChromaDB, and PostgreSQL are running:

```powershell
docker compose ps
Invoke-RestMethod http://localhost:5260/health/ready
docker compose exec -T redis redis-cli ping
```

Expected results are a healthy API response and `PONG` from Redis. One-shot services such as `finance-init` and `rag-init` may have completed and exited; that is normal.

## How To Observe Cache Behavior

Every cache layer now writes a safe structured API log line:

```text
Application cache Miss. CacheKey: ...; TtlSeconds: ...
Application cache Hit. CacheKey: ...; TtlSeconds: ...
Application cache invalidated. CacheKey: ...
Application cache failed open. CacheKey: ...; FailureType: ...
Application cache bypassed. CacheKey: ...; Reason: Disabled.
```

The keys contain operation names, canonical dates, or hashes. They never contain raw prompts, retrieved text, tool arguments, response values, secrets, or final chat responses.

Open one PowerShell window for logs:

```powershell
docker compose logs -f --no-color api | Select-String 'Application cache'
```

Open another window to inspect Redis safely. Use `SCAN`, not `KEYS`, because `SCAN` does not block Redis:

```powershell
docker compose exec -T redis redis-cli --scan --pattern '*finance*'
docker compose exec -T redis redis-cli --scan --pattern '*rag*'
docker compose exec -T redis redis-cli --scan --pattern '*embedding*'
docker compose exec -T redis redis-cli --scan --pattern '*mcp-discovery*'
docker compose exec -T redis redis-cli --scan --pattern '*llm-classification*'
```

To check a key expiry, copy one returned key and run:

```powershell
docker compose exec -T redis redis-cli TTL '<copied-key>'
```

Redis has no persistent application-data volume. The following optional reset clears only cached entries; it does not affect PostgreSQL, ChromaDB, source documents, or finance data:

```powershell
docker compose exec -T redis redis-cli FLUSHDB
```

## Reusable API Request

Do not send `conversationId` for the repeated-request tests. Omitting it keeps classification stateless, so the second request has the same classification-cache key.

```powershell
$message = 'Give me the sales summary of this week.'
$requestBody = @{ message = $message } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5260/api/chat -ContentType 'application/json' -Body $requestBody
```

Run the same command a second time immediately when a test says to repeat it.

## Test Cases

### TC-01: Verify Redis And Safe Cache Keys

1. Run `docker compose exec -T redis redis-cli ping`.
2. Run the optional `FLUSHDB` command above.
3. Send the reusable API request once.
4. Scan for `*finance*` and `*llm-classification*` keys.
5. Inspect the API cache logs.

Expected behavior:

- Redis returns `PONG`.
- The first request produces cache `Miss` lines for LLM classification and the requested Finance operation.
- Redis contains matching cache keys.
- No key contains the full user message, a connection string, a secret, or finance result values.

### TC-02: LLM Intent Classification Cache

1. Run `FLUSHDB`.
2. Set `$message` to `Give me the sales summary of this week.` and run the reusable API request twice.
3. In the API log window, find keys containing `llm-classification`.
4. Scan Redis for `*llm-classification*`.

Expected behavior:

- First request: `llm-classification` is a `Miss`.
- Second identical stateless request: the same `llm-classification` key is a `Hit`.
- The final response is still generated normally; only intent classification is cached.
- Unsupported, malformed, blocked, failed, or cancelled classifications do not create reusable entries.

### TC-03: Finance MCP Read Cache

1. Run `FLUSHDB`.
2. Send `Give me the sales summary of this week.` twice without `conversationId`.
3. Find `finance:v1` cache log lines.
4. Optionally inspect Finance MCP logs in a second window:

```powershell
docker compose logs -f --no-color finance-mcp
```

Expected behavior:

- First request: the requested `finance:v1` key is a `Miss` and Finance MCP is called.
- Second request: the same Finance key is a `Hit`; it returns the previously verified typed result without another Finance MCP read.
- The LLM is not used to calculate the financial values in either request.

Use these prompts to exercise the other finance cache operations:

| Prompt | Expected operation marker |
| --- | --- |
| `Compare this week's sales with last week.` | `week-over-week-comparison` |
| `Show me the top five products this month.` | `current-month-top-products` |
| `Give me the sales forecast for the next five years.` | `historical-yearly-totals` |

### TC-04: RAG Retrieval Cache

1. Run `FLUSHDB`.
2. Set `$message` to `What documented market risks should leadership review?`.
3. Send the request twice without `conversationId`.
4. Find `rag:v1:retrieval` and `embedding:v1` log lines.
5. Scan Redis for `*rag*`.

Expected behavior:

- First request: RAG retrieval is a `Miss`; it queries ChromaDB and preserves returned source metadata/citations.
- Second request: the same RAG retrieval key is a `Hit`.
- The response continues to show grounded knowledge sources when the indexed documents contain relevant content.
- ChromaDB remains the semantic retrieval system; this cache does not replace it with file reads.

### TC-05: Embedding Cache Without A RAG Retrieval Hit

This test temporarily shortens only the RAG retrieval TTL so the same question must perform retrieval again while reusing its query embedding.

1. Run `FLUSHDB`.
2. In `.env`, change `CACHE_RAG_RETRIEVAL_TTL_SECONDS=300` to `CACHE_RAG_RETRIEVAL_TTL_SECONDS=1`.
3. Recreate only the API container so it receives the changed environment:

```powershell
docker compose up -d --force-recreate api
```

4. Send `What documented market risks should leadership review?` once.
5. Wait at least two seconds.
6. Send the same request again without `conversationId`.
7. Check the API logs.
8. Restore `CACHE_RAG_RETRIEVAL_TTL_SECONDS=300` in `.env` and run `docker compose up -d --force-recreate api` again.

Expected behavior:

- First request: `rag:v1:retrieval` and the query `embedding:v1` key are `Miss` entries.
- After the one-second RAG TTL expires, the second request shows a RAG `Miss` but an embedding `Hit`.
- This proves the embedding vector is cached independently of the retrieval result.

### TC-06: Shared MCP Tool-Discovery Cache Across An API Restart

1. Run `FLUSHDB`.
2. Send the sales-summary request once. Confirm an `mcp-discovery` `Miss` in API logs.
3. Remove only Finance result keys while retaining MCP discovery keys:

```powershell
$financeKeys = docker compose exec -T redis redis-cli --scan --pattern '*finance:v1*'
foreach ($cacheKey in $financeKeys) {
    docker compose exec -T redis redis-cli DEL $cacheKey
}
```

4. Recreate only the API container:

```powershell
docker compose up -d --force-recreate api
```

5. Send the same sales-summary request again.
6. Check API cache logs for `mcp-discovery`.

Expected behavior:

- The API creates a new MCP SDK client and completes the required MCP handshake after restart.
- The approved tool-name snapshot is a distributed `mcp-discovery` `Hit`, so the adapter can avoid a fresh `tools/list` request.
- The Finance result is a `Miss` because only its keys were removed, proving the MCP adapter was exercised again.
- Tool calls remain restricted to the configured allow-list.

### TC-07: TTL Expiration

1. In `.env`, temporarily set `CACHE_FINANCE_SALES_SUMMARY_TTL_SECONDS=5`.
2. Run `docker compose up -d --force-recreate api`.
3. Run `FLUSHDB`.
4. Send the sales-summary request twice immediately, then once more after waiting at least six seconds.
5. Restore the finance TTL to `60` and recreate the API container again.

Expected behavior:

- First request: Finance `Miss`.
- Immediate second request: Finance `Hit`.
- Request after six seconds: Finance `Miss` because the entry expired.

### TC-08: Cache Disabled Bypass

1. Set `CACHE_ENABLED=false` in `.env`.
2. Run `docker compose up -d --force-recreate api`.
3. Send the same sales-summary request twice.
4. Inspect API logs.
5. Restore `CACHE_ENABLED=true` and recreate the API container.

Expected behavior:

- Each cacheable operation logs `Application cache bypassed`.
- Both requests still return correct answers through the authoritative dependencies.
- Redis is not required for the API to produce the answers while caching is disabled.

### TC-09: Redis Failure Fails Open

1. Ensure `CACHE_ENABLED=true` and recreate the API container.
2. Restart the API to clear its process-local cache without restarting Redis:

```powershell
docker compose restart api
```

3. Stop Redis:

```powershell
docker compose stop redis
```

4. Send the sales-summary request.
5. Inspect API logs for `Application cache failed open`.
6. Restore Redis and verify health:

```powershell
docker compose start redis
docker compose ps
docker compose exec -T redis redis-cli ping
```

Expected behavior:

- The request remains functional through Finance MCP despite the cache failure.
- API logs show a sanitized failure type only; they do not expose a Redis connection string or response data.
- After Redis starts, it returns `PONG` and later requests can cache normally.

## Cleanup After Testing

1. Restore every temporary TTL and `CACHE_ENABLED` value in `.env` to the values in `.env.example`.
2. Recreate the API one last time:

```powershell
docker compose up -d --force-recreate api
```

3. Confirm normal operation:

```powershell
docker compose ps
Invoke-RestMethod http://localhost:5260/health/ready
```

You may leave Redis cache entries in place. To start cache verification from a clean state later, use `FLUSHDB`; it clears only the disposable Redis cache.
