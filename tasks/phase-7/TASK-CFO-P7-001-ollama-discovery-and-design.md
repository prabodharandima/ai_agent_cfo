# TASK-CFO-P7-001 — Ollama Discovery and Design

## Objective

Produce a repository-grounded design for adding Ollama with `llama3.2:3b` while retaining the Mock provider.

## Mandatory reading

Read completely:

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- `EXECUTION-ORDER.md`
- `tasks/phase-7/PHASE-7-EXECUTION-ORDER.md`
- this task file

Inspect the actual repository before making changes.

## Scope

Documentation only. Create `docs/PHASE-7-OLLAMA-DESIGN.md`.

## Out of scope

Runtime code, configuration changes, package changes, tests, embeddings migration, OpenAI integration.

## Requirements

The design must identify the actual current `IChatClient` registration, Mock implementation, agent consumers, prompt/formatting paths, configuration validation, health/readiness design, error mapping, and test seams. Propose the smallest compatible Ollama integration. Decide—based on official compatible APIs available in the repository environment—whether to use a Microsoft-compatible Ollama package or a small typed HTTP adapter. Document provider selection (`Mock` or `Ollama`), `llama3.2:3b` configuration, timeouts, cancellation, offline tests, opt-in live tests, and the unchanged deterministic finance/RAG/MCP boundaries. Clearly separate current behavior from proposed behavior.

## Acceptance criteria

- The design references actual repository classes and paths.
- No runtime file is changed.
- Mock, MCP, ChromaDB, current embeddings, and deterministic calculations are preserved.
- No speculative provider beyond Ollama is designed.

## Validation

Run focused tests, then:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
```

## Completion report

Report files created/modified, design decisions, commands, focused tests, total test count, build result, assumptions, deviations, blockers, and whether every acceptance criterion is complete.

Stop after this task. Do not start the next task.

