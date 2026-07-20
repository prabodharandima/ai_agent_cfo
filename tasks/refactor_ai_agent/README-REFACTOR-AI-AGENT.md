# CfoAgent.Api Architecture Review and Refactor Task Pack

## Goal

Review only the `CfoAgent.Api` project and determine whether it is unnecessarily complex, over-engineered, or inconsistent with the intended architecture:

- Orchestrator–Worker multi-agent pattern
- Clean/Hexagonal Architecture principles
- Simple, explicit, testable application flow
- Clear ports for LLM, MCP, and vector database integrations

Refactor only when evidence shows the current implementation needs it.

## Target responsibility of CfoAgent.Api

The API should:

1. Receive a user prompt.
2. Use an LLM or deterministic provider to understand the request.
3. Coordinate one or more specialist agents.
4. Invoke approved MCP tools through an adapter.
5. Query the vector database through an adapter when needed.
6. Compose and return the final result.
7. Preserve deterministic financial calculations where applicable.

## Non-goals

- Do not redesign MCP server projects.
- Do not redesign the UI.
- Do not change MCP tool contracts.
- Do not change PostgreSQL ownership.
- Do not replace ChromaDB.
- Do not introduce a workflow engine, mediator framework, event bus, plugin registry, policy engine, or custom agent framework.
- Do not create abstractions only for theoretical flexibility.
- Do not add microservices.
- Do not perform unrelated cleanup.

## Simplicity rules

- Prefer fewer layers and fewer abstractions.
- Keep one clear orchestrator.
- Keep specialist agents only where they represent real responsibilities.
- Use ports/adapters only for external dependencies or meaningful boundaries.
- Avoid interfaces with one trivial implementation unless testing or replacement value is clear.
- Avoid wrapper-on-wrapper designs.
- Avoid generic base classes unless multiple implementations genuinely share behavior.
- Avoid unnecessary factories, registries, strategies, decorators, pipelines, and handlers.
- Keep public API contracts unchanged.
- Keep the project buildable after every task.

## Execution order

1. `TASK-RAA-001-current-architecture-and-complexity-assessment.md`
2. `TASK-RAA-002-target-architecture-and-refactor-plan.md`
3. `TASK-RAA-003-simplify-orchestrator-and-agent-boundaries.md`
4. `TASK-RAA-004-simplify-ports-adapters-and-dependency-flow.md`
5. `TASK-RAA-005-remove-accidental-complexity-and-dead-abstractions.md`
6. `TASK-RAA-006-regression-documentation-and-final-gate.md`

Run strictly in order and stop after each task.
