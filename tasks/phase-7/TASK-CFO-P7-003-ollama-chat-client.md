# TASK-CFO-P7-003 — Ollama Chat Client

## Objective

Implement an Ollama-backed `IChatClient` for `llama3.2:3b`.

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

Ollama chat transport/adapter, request mapping, response parsing, metadata, cancellation, timeout, sanitized failures.

## Out of scope

Live integration tests, embeddings, frontend, OpenAI, model-driven MCP tool execution.

## Requirements

Use the smallest compatible implementation from Task P7-001. Send configured model and bounded generation options. Keep transport-specific types out of agents. Return provider metadata `Ollama` and the configured model. Propagate caller cancellation. Apply a finite timeout. Never retry indefinitely. Convert transport failures into controlled provider exceptions. Never log full prompts, RAG context, documents, or response bodies. Do not let the model calculate authoritative finance values or directly execute MCP tools. Keep all normal tests offline through a fake HTTP handler or equivalent seam.

## Acceptance criteria

- A fake-transport test can complete a chat request through `IChatClient`.
- Metadata is correct.
- Cancellation and timeout behave correctly.
- Non-success and malformed responses are sanitized.
- Mock behavior is unchanged.
- No test requires Ollama.

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

