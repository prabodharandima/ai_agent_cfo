# TASK-RAA-004 — Simplify Ports, Adapters, and Dependency Flow

## Objective

Apply Clean/Hexagonal boundaries around LLM, MCP, and vector-store integrations while removing unnecessary adapter layers.

## Prerequisites

- TASK-RAA-003 complete
- Orchestrator and worker boundaries are stable

## Scope

Refactor only within `CfoAgent.Api`:

- LLM port and adapters
- MCP tool adapter boundary
- vector-store/RAG boundary
- DI registration
- configuration binding
- error translation
- related tests

## Requirements

- Prefer one meaningful port per external dependency type.
- Reuse existing SDK types when practical.
- Remove wrapper-on-wrapper layers.
- Keep provider-specific code out of orchestrator and specialist agents.
- Keep MCP transport details out of agents.
- Keep ChromaDB-specific code out of agents.
- Keep Ollama-specific code out of agents.
- Preserve Mock provider support.
- Preserve sanitized MCP dependency failures.
- Preserve cancellation.
- Preserve health checks and configuration.
- Do not change MCP server contracts.
- Do not change vector DB behavior.
- Do not introduce custom frameworks.

## Tests required

Prove:

- agent code can be tested with test doubles
- Mock and Ollama providers remain selectable
- MCP failures map correctly
- vector search failures map correctly
- cancellation propagates
- DI resolves the complete application
- no API project code accesses PostgreSQL directly

## Acceptance criteria

- Dependency flow points inward.
- External integrations are behind small meaningful ports.
- No unnecessary adapter chain remains.
- Public behavior remains unchanged.
- Full tests pass.

## Validation

```bash
dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
git diff --check
```

Run existing container integration tests if applicable.

Stop after this task.
