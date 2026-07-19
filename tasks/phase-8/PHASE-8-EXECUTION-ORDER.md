# Phase 8 Execution Order

| Order | Task | Outcome |
|---:|---|---|
| 1 | `TASK-CFO-P8-001-architecture-discovery-and-migration-design.md` | Target architecture and migration map |
| 2 | `TASK-CFO-P8-002-contract-freeze-and-baseline-gate.md` | Frozen contracts and verified baseline |
| 3 | `TASK-CFO-P8-003-move-finance-ownership-to-finance-mcp.md` | Finance ownership moved out of API |
| 4 | `TASK-CFO-P8-004-postgresql-migrations-and-seeding.md` | PostgreSQL persistence owned by Finance MCP |
| 5 | `TASK-CFO-P8-005-finance-mcp-streamable-http-host.md` | Finance MCP hosted over network MCP transport |
| 6 | `TASK-CFO-P8-006-knowledge-mcp-streamable-http-host.md` | Knowledge MCP hosted over network MCP transport |
| 7 | `TASK-CFO-P8-007-api-http-mcp-clients-and-failure-policy.md` | API uses remote MCP endpoints; no Finance fallback |
| 8 | `TASK-CFO-P8-008-remove-finance-persistence-from-api.md` | API no longer owns finance persistence |
| 9 | `TASK-CFO-P8-009-backend-containers-and-compose.md` | Backend/infrastructure containers |
| 10 | `TASK-CFO-P8-010-container-integration-and-resilience-tests.md` | Real container integration gate |
| 11 | `TASK-CFO-P8-011-frontend-container-and-full-deployment.md` | Full local deployment |
| 12 | `TASK-CFO-P8-012-documentation-cleanup-and-phase-gate.md` | Updated docs and final gate |

Execute tasks strictly in order. Stop and commit after each successful task.
