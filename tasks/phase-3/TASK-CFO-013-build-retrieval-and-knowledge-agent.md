# TASK-CFO-013 — Build RAG retrieval and Financial Knowledge Agent

## Phase

Phase 3 — RAG and orchestration

## Goal

Answer document-based financial questions using retrieved chunks and source citations.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- `TASK-CFO-012` completed.

## Implementation steps


1. Implement `FinancialKnowledgeRetrievalService`.
2. Accept query, top-K, and optional document type/period filters.
3. Generate the query embedding and retrieve relevant Chroma chunks.
4. Map results to typed source records.
5. Implement `FinancialKnowledgeAgent`.
6. The agent must answer only from retrieved chunks.
7. Return source name, section, period, and path for each used chunk.
8. Return an explicit insufficient-knowledge answer when results are empty or weak.
9. Keep raw retrieved content size bounded before sending to the mock chat client.


## Expected files or areas


- `src/CfoAgent.Api/Rag/Retrieval/`
- `src/CfoAgent.Api/Agents/FinancialKnowledgeAgent.cs`


## Acceptance criteria


- Budget-target and assumptions questions return relevant sources.
- Missing knowledge does not produce fabricated content.
- Duplicate source citations are removed.
- Retrieval output is deterministic for the demo dataset.


## Validation commands

```bash
docker compose up -d
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
```

## Constraints and non-goals

- Do not add web search or external document systems.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-013 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
