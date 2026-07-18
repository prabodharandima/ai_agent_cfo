# TASK-CFO-P7-008 — Documentation and Phase Gate

## Objective

Finalize documentation and verify the complete Phase 7 Ollama integration.

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

Update README/architecture/configuration documentation and create `docs/PHASE-7-RESULTS.md`.

## Out of scope

New runtime features, OpenAI, embedding migration, cloud deployment, authentication, unrelated refactoring.

## Requirements

Document Mock and Ollama provider selection, `llama3.2:3b`, prerequisites, configuration, startup, readiness, timeout/cancellation, CPU-oriented latency expectations without fabricated benchmarks, deterministic finance guardrails, unchanged embeddings/RAG/MCP behavior, offline tests, opt-in live tests, switching back to Mock, and troubleshooting. Update `APPLICATION_ARCHITECTURE.md` if present. Update AGENT.md/IMPLEMENTATION-PLAN.md only where necessary and preserve unrelated content. `docs/PHASE-7-RESULTS.md` must record implemented scope, decisions, exact commands/counts, Debug/Release results, live-test result or skip reason, limitations, deviations, blockers, and final gate status.

## Acceptance criteria

- Documentation matches code/configuration.
- `llama3.2:3b` is configuration-driven.
- No claim assigns finance calculations to Ollama.
- No claim says embeddings changed.
- Debug and Release gates pass.
- Phase 7 status is explicit.
- No OpenAI/Phase 8 work begins.

## Validation

Run:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
```

Run the opt-in Ollama tests when locally available; otherwise document the exact skip reason.

## Completion report

Report files created/modified, design decisions, commands, focused tests, total test count, build result, assumptions, deviations, blockers, and whether every acceptance criterion is complete.

Stop after this task. Do not start the next task.

