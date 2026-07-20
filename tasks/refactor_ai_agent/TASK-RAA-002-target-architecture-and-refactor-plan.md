# TASK-RAA-002 — Target Architecture and Refactor Plan

## Objective

Define a minimal, repository-grounded target architecture for `CfoAgent.Api` using the Orchestrator–Worker pattern with Clean/Hexagonal principles.

## Prerequisite

- TASK-RAA-001 complete
- `docs/CFO-AGENT-API-CURRENT-ARCHITECTURE-ASSESSMENT.md` exists

## Scope

Create:

- `docs/CFO-AGENT-API-TARGET-ARCHITECTURE.md`
- `docs/CFO-AGENT-API-REFACTOR-PLAN.md`

Do not modify runtime code.

## Target architecture

```text
HTTP Endpoint
  -> CFO Orchestrator
     -> Specialist Agent(s)
        -> LLM Port
        -> MCP Tool Adapter Port
        -> Vector Search Port
     -> Result Composer
  -> HTTP Response
```

## Required design decisions

Define:

1. Exact responsibility of the HTTP endpoint.
2. Exact responsibility of the CFO orchestrator.
3. Exact responsibility of each specialist agent.
4. When multiple agents are justified.
5. How mixed requests are handled.
6. Which logic stays deterministic.
7. LLM port and adapter boundary.
8. MCP port and adapter boundary.
9. Vector-store port and adapter boundary.
10. Result composition.
11. Error and cancellation flow.
12. DI registrations.
13. Classes/interfaces to retain.
14. Classes/interfaces to remove or merge.
15. Files to move, rename, or delete.
16. Migration steps in safe order.
17. Tests required for every step.

## Simplicity constraints

- One orchestrator.
- Specialist agents only for distinct responsibilities.
- No mediator framework.
- No event bus.
- No workflow engine.
- No plugin registry.
- No custom agent framework.
- No generic repository pattern.
- No unnecessary unit-of-work abstraction.
- No layered wrappers around MCP/LLM/vector clients.
- No abstractions without concrete testing or replacement value.
- Preserve public API contracts.
- Preserve behavior.
- Preserve current MCP, vector-store, and LLM integrations.

## Acceptance criteria

- Target architecture is understandable on one page.
- Dependency direction is explicit.
- Every proposed abstraction has a reason.
- Every proposed deletion has evidence.
- Refactor plan is incremental and testable.
- No runtime code changed.

## Validation

```bash
git status
git diff --check
```

Confirm only the two design documents changed.

Stop after this task.
