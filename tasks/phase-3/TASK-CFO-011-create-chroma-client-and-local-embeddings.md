# TASK-CFO-011 — Create Chroma client and deterministic local embeddings

## Phase

Phase 3 — RAG and orchestration

## Goal

Provide a minimal vector-store integration that works without a paid embedding model.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 2 gate passed and ChromaDB can run locally.

## Implementation steps


1. Define a small `IEmbeddingGenerator` abstraction only if the current Microsoft AI abstraction cannot be used directly.
2. Implement a deterministic token-hashing embedding generator with a fixed dimension.
3. Normalize vectors and document that this is for plumbing/testing, not production semantic quality.
4. Implement a focused Chroma client using a maintained compatible package or direct HTTP calls.
5. Support:
   - health/heartbeat,
   - create/get collection,
   - upsert chunks with embeddings and metadata,
   - query top-K with an embedding.
6. Add typed Chroma options and timeouts.
7. Do not hide failures; return controlled dependency exceptions.


## Expected files or areas


- `src/CfoAgent.Api/Rag/Embeddings/`
- `src/CfoAgent.Api/Rag/Chroma/`
- configuration/DI updates


## Acceptance criteria


- Equal text produces equal vectors.
- Similar token content has usable retrieval behavior for the demo documents.
- Chroma collection operations work against Docker.
- No external embedding API is called.


## Validation commands

```bash
docker compose up -d
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not build a production-grade NLP embedding model.
- Do not add multiple vector databases.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-011 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
