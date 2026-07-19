# Execution Order

Run exactly one task at a time.

> Historical index: this file records the original Phase 0-6 sequence. Later work has its own execution orders: [Phase 7](tasks/phase-7/PHASE-7-EXECUTION-ORDER.md), [Phase 8](tasks/phase-8/PHASE-8-EXECUTION-ORDER.md), and [cleanup](tasks/cleanup/CLEANUP-EXECUTION-ORDER.md).

| Order | Task |
|---:|---|
| 1 | `tasks/phase-0/TASK-CFO-000-repository-discovery-and-guardrails.md` |
| 2 | `tasks/phase-0/TASK-CFO-001-create-solution-api-tests-and-react.md` |
| 3 | `tasks/phase-0/TASK-CFO-002-local-infrastructure-configuration-and-health.md` |
| 4 | `tasks/phase-1/TASK-CFO-003-create-finance-data-model-and-sqlite.md` |
| 5 | `tasks/phase-1/TASK-CFO-004-seed-demo-data-and-knowledge-documents.md` |
| 6 | `tasks/phase-1/TASK-CFO-005-implement-finance-analysis-and-forecasting.md` |
| 7 | `tasks/phase-1/TASK-CFO-006-phase-1-core-tests.md` |
| 8 | `tasks/phase-2/TASK-CFO-007-implement-deterministic-mock-chat-client.md` |
| 9 | `tasks/phase-2/TASK-CFO-008-configure-agent-framework-and-contracts.md` |
| 10 | `tasks/phase-2/TASK-CFO-009-implement-sales-and-forecast-agents.md` |
| 11 | `tasks/phase-2/TASK-CFO-010-phase-2-agent-tests.md` |
| 12 | `tasks/phase-3/TASK-CFO-011-create-chroma-client-and-local-embeddings.md` |
| 13 | `tasks/phase-3/TASK-CFO-012-build-rag-document-ingestion.md` |
| 14 | `tasks/phase-3/TASK-CFO-013-build-retrieval-and-knowledge-agent.md` |
| 15 | `tasks/phase-3/TASK-CFO-014-implement-cfo-orchestrator.md` |
| 16 | `tasks/phase-3/TASK-CFO-015-phase-3-rag-and-orchestration-tests.md` |
| 17 | `tasks/phase-4/TASK-CFO-016-build-finance-mcp-server.md` |
| 18 | `tasks/phase-4/TASK-CFO-017-connect-mcp-clients.md` |
| 19 | `tasks/phase-4/TASK-CFO-018-phase-4-mcp-tests.md` |
| 20 | `tasks/phase-5/TASK-CFO-019-create-chat-api.md` |
| 21 | `tasks/phase-5/TASK-CFO-020-build-react-chat-ui.md` |
| 22 | `tasks/phase-5/TASK-CFO-021-phase-5-integration-and-e2e-tests.md` |
| 23 | `tasks/phase-6/TASK-CFO-022-hardening-and-observability.md` |
| 24 | `tasks/phase-6/TASK-CFO-023-final-regression-readme-and-demo.md` |

## Phase gates

Do not continue when one of these tasks fails:

- `TASK-CFO-006`
- `TASK-CFO-010`
- `TASK-CFO-015`
- `TASK-CFO-018`
- `TASK-CFO-021`
- `TASK-CFO-023`
