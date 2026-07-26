# TASK-MAF-006 — RAG Context Provider

## Goal

Integrate the existing ChromaDB retrieval path with a Microsoft Agent Framework context provider while preserving current behavior.

## Before editing

Read repository instructions, the integration README, all earlier result files, Git history, and current code.

## Requirements

Move only context acquisition and bounded-context preparation into the context-provider integration.

Preserve:

- `IFinancialKnowledgeSearch`;
- ChromaDB retrieval;
- current threshold;
- metadata checks;
- overlap/duplicate handling;
- citations;
- maximum context length;
- insufficient-knowledge behavior;
- treatment of retrieved content as untrusted data.

Do not change embeddings, ChromaDB, API contracts, or raw context logging.

## Tests

Cover:

- relevant retrieval;
- bounded context;
- citation preservation;
- suspicious retrieved instructions;
- insufficient knowledge without LLM call;
- cancellation;
- unchanged knowledge response contract.

Run focused tests, build, full tests, and `git diff --check`.

## Documentation

After tests pass, update only the actual RAG flow and limitations.

## Result file

Create `TASK-MAF-006-RESULT.md`.
