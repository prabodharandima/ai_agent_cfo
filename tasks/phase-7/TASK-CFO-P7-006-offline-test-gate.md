# TASK-CFO-P7-006 — Offline Test Gate

## Objective

Complete deterministic offline coverage for the Ollama path.

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

Provider selection, options, transport, agent behavior, health, API compatibility, Mock regression.

## Out of scope

Real Ollama dependency, performance tests, frontend E2E with live Ollama.

## Requirements

Cover: Mock/Ollama/unsupported provider selection; option validation; request serialization for `llama3.2:3b`; response parsing; metadata; cancellation; timeout; non-success and malformed responses; readiness; sanitized Problem Details; no startup network call; agent routing with Ollama-style outputs; malformed classification fallback; no invented financial values; RAG sources retained; MCP unchanged; Mock regression. Use fakes/stubs only. Fix implementation defects with the smallest compatible change and do not weaken tests.

## Acceptance criteria

- The default full test suite remains completely offline.
- Every listed behavior has deterministic coverage.
- Mock and Ollama paths pass.
- Existing tests are not deleted or weakened.

## Validation

Run:

```bash
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
```

Record exact Debug and Release counts.

## Completion report

Report files created/modified, design decisions, commands, focused tests, total test count, build result, assumptions, deviations, blockers, and whether every acceptance criterion is complete.

Stop after this task. Do not start the next task.

