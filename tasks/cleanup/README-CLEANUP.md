# CFO AI Agent Cleanup Task Pack

## Purpose

This task pack guides Codex through a safe repository cleanup after the Phase 8 refactor.

A single large cleanup prompt is not recommended because unused-code detection can produce false positives and because backend, frontend, Docker, tests, and documentation require different validation paths.

## Binding cleanup rules

1. Do not delete anything unless its lack of use is proven.
2. Treat reflection, dependency injection, configuration binding, serialization, EF Core migrations, MCP tool discovery, health checks, CLI commands, and test-only usage as real usage.
3. Preserve current architecture and behavior.
4. Do not change public API contracts or approved MCP tool names.
5. Do not redesign the solution.
6. Do not weaken tests.
7. Prefer removing dead code over refactoring working code.
8. Keep every task independently buildable and testable.
9. Use serialized .NET validation.
10. Commit after each successful task.

## Execution order

1. `TASK-CLEANUP-001-repository-inventory-and-cleanup-plan.md`
2. `TASK-CLEANUP-002-backend-and-mcp-cleanup.md`
3. `TASK-CLEANUP-003-frontend-cleanup.md`
4. `TASK-CLEANUP-004-infrastructure-tests-and-documentation-cleanup.md`
5. `TASK-CLEANUP-005-final-regression-and-cleanup-gate.md`
