# Phase 8 Results

## Status

**Complete.** Phase 8 replaced API-owned SQLite and stdio child-process MCP connections with two independently hosted Streamable HTTP MCP services, Finance MCP-owned PostgreSQL, and a complete Docker Compose deployment including the React/Nginx frontend.

## Delivered architecture

- `CfoAgent.Api` remains the AI/orchestration monolith with four in-process agents and Mock/Ollama `IChatClient` providers.
- Finance MCP owns PostgreSQL migrations, deterministic seed data, queries, aggregation, and budget lookup. API finance persistence and the local Finance fallback are removed.
- Finance MCP dependency loss is a sanitized HTTP 503. Caller cancellation is propagated.
- Knowledge File MCP exposes only read-only list/read operations under the mounted knowledge root. Its secure local fallback is Development-only and disabled in Compose.
- ChromaDB remains the semantic RAG and citation store. Ollama remains on the Windows host and is reached by API containers through `host.docker.internal`.
- Docker publishes frontend `5173` and diagnostic API `5260`; PostgreSQL, ChromaDB, and both MCP services remain internal. PostgreSQL and ChromaDB use named volumes. Knowledge files are mounted read-only.

## Final gate results

All final gates completed successfully on 2026-07-19.

| Gate | Result |
|---|---|
| Debug solution build | Passed, 0 warnings and 0 errors |
| Debug solution tests | 176 total: 168 passed, 8 skipped, 0 failed |
| Release solution tests | 176 total: 168 passed, 8 skipped, 0 failed |
| Frontend unit tests | 10 passed across 2 test files |
| Frontend build | Passed |
| Playwright against frontend container | 7 passed in Chromium |
| Isolated MCP/container resilience gate | 3 passed; seed, RAG, outage, recovery, ports, and mount checks passed |

The eight skipped backend tests are opt-in: three real container tests, four live Ollama tests, and one legacy localhost Chroma integration test. The container gate executes the three container tests separately against its isolated Compose project.

## Historical records

`PHASE-8-BASELINE.md` and `PHASE-8-MIGRATION-DESIGN.md` describe the pre-migration repository and the migration plan. They are preserved for traceability and are superseded by this results document, the active architecture document, and the current Compose configuration.

## Troubleshooting

- Run `docker compose ps --all` and `docker compose logs --no-color` if a dependency is not healthy.
- Ensure Docker Desktop is running and ports `5173` and `5260` are available.
- Use `docker compose down` to stop services while preserving volumes. Use `docker compose down -v` only when intentionally resetting PostgreSQL and ChromaDB.
- The container gate uses an isolated Compose project and removes only its own test volumes. Run `scripts/test-phase-8-containers.ps1` from the repository root.
- For optional Ollama, start it on the Windows host, pull `llama3.2:3b` manually, and select the provider through configuration. Normal tests remain offline and do not require it.
