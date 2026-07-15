# TASK-CFO-012 — Build RAG document ingestion

## Phase

Phase 3 — RAG and orchestration

## Goal

Ingest the demo financial documents into ChromaDB with useful metadata and idempotency.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-011` completed.

## Implementation steps


1. Read Markdown documents from `data/knowledge`.
2. Parse source metadata from front matter or manifest.
3. Chunk by headings and bounded paragraph size; avoid tiny arbitrary chunks.
4. Generate deterministic embeddings.
5. Upsert using stable chunk IDs based on document path and content/hash.
6. Store document name, type, period, section, and source path as metadata.
7. Add a development CLI argument or endpoint to run ingestion.
8. Return ingestion counts: documents, chunks added/updated, skipped, failed.
9. Re-running ingestion must not create duplicates.


## Expected files or areas


- `src/CfoAgent.Api/Rag/Ingestion/`
- ingestion command/endpoint
- README usage


## Acceptance criteria


- All demo documents are ingested.
- Chroma contains stable IDs and source metadata.
- Second run is idempotent.
- Failures identify the source document.


## Validation commands

```bash
docker compose up -d
dotnet run --project src/CfoAgent.Api -- --ingest-rag
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not ingest individual raw sales transactions into Chroma.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-012 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
