# TASK-CFO-P7-004 — Agent Integration and Guardrails

## Objective

Make the existing four-agent workflow operate with Mock or Ollama while keeping deterministic routing, data authority, MCP usage, and RAG grounding.

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

Existing orchestration, prompts, response formatting, provider metadata, malformed-output handling.

## Out of scope

New agents, autonomous loops, direct model-driven MCP calls, finance formula changes, embedding changes, frontend redesign.

## Requirements

Preserve the four agents and maximum specialist-agent limit. Preserve deterministic routing where already implemented. If classification uses `IChatClient`, require bounded machine-readable output and a deterministic safe fallback for malformed output. Supply only verified structured financial values to Ollama. Never parse authoritative values back from free-form text. Preserve RAG citations and insufficient-knowledge behavior. Existing specialist/application code—not Ollama—continues to select MCP operations. Use concise prompts suitable for a 3B model and bound context size.

## Acceptance criteria

- All five MVP scenarios work with an Ollama-style fake client.
- Malformed classification/structured output follows a safe deterministic path.
- Finance values remain service/MCP outputs.
- RAG remains grounded and cited.
- Public API compatibility and Mock tests are preserved.
- No direct model-driven MCP execution is added.

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

