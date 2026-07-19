# Cleanup Execution Order

| Order | Task | Outcome |
|---:|---|---|
| 1 | `TASK-CLEANUP-001-repository-inventory-and-cleanup-plan.md` | Evidence-based cleanup inventory |
| 2 | `TASK-CLEANUP-002-backend-and-mcp-cleanup.md` | Remove proven dead .NET/MCP code and dependencies |
| 3 | `TASK-CLEANUP-003-frontend-cleanup.md` | Remove proven dead React/TypeScript/CSS code and packages |
| 4 | `TASK-CLEANUP-004-infrastructure-tests-and-documentation-cleanup.md` | Clean stale scripts, Docker, test artifacts, and docs |
| 5 | `TASK-CLEANUP-005-final-regression-and-cleanup-gate.md` | Full regression and cleanup completion report |

Run tasks strictly in order and stop after each task.
