# Phase 3 Results

## Scope

Phase 3 covers deterministic Chroma embeddings, Markdown ingestion, grounded knowledge retrieval, the Financial Knowledge Agent, and the in-process CFO orchestrator.

## Gate Coverage

- Deterministic token-hash embeddings produce stable, normalized vectors.
- Markdown ingestion tests cover heading-aware chunking, stable IDs, metadata, idempotent upserts, and source-specific failures.
- Offline retrieval tests cover annual target, forecast assumptions, market risks, product strategy, source metadata, citation deduplication, and insufficient knowledge.
- Orchestrator tests cover all five MVP prompt intents, unsupported requests, and the two-agent forecast-with-assumptions route.
- `ChromaPhaseThreeIntegrationTests` ingests the five real demo documents into a unique Chroma collection and retrieves each knowledge topic when Docker is available. It skips with an explicit reason when Chroma cannot be reached.

## Validation

Executed on 2026-07-16:

```powershell
docker compose up -d
```

Result: passed. The local `chromadb` container started and its heartbeat endpoint responded.

```powershell
dotnet test CfoAgent.sln --configuration Release
```

Result: the complete test project passed with 68 passed, 0 failed, and 0 skipped. This includes `ChromaPhaseThreeIntegrationTests.IngestsDemoDocumentsAndRetrievesEachKnowledgeTopic` against the local ChromaDB container. The solution-level runner stalled in this shell after Docker startup, so the complete test project was run directly to obtain the final result.

## Gate Status

Passed. Docker-backed Chroma validation completed without skips; Phase 4 may proceed.
