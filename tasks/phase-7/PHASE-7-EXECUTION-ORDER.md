# Phase 7 Execution Order

Execute the tasks below sequentially. Do not begin a later task until the current task satisfies every acceptance criterion.

| Order | Task | Result |
|---:|---|---|
| 1 | `TASK-CFO-P7-001-ollama-discovery-and-design.md` | Repository-grounded design |
| 2 | `TASK-CFO-P7-002-ollama-options-and-registration.md` | Configuration and provider selection |
| 3 | `TASK-CFO-P7-003-ollama-chat-client.md` | Ollama-backed `IChatClient` |
| 4 | `TASK-CFO-P7-004-agent-integration-and-guardrails.md` | Existing agents support Ollama safely |
| 5 | `TASK-CFO-P7-005-resilience-health-and-logging.md` | Operational behavior |
| 6 | `TASK-CFO-P7-006-offline-test-gate.md` | Deterministic offline regression gate |
| 7 | `TASK-CFO-P7-007-live-ollama-integration-tests.md` | Opt-in real local validation |
| 8 | `TASK-CFO-P7-008-documentation-and-phase-gate.md` | Documentation and completion report |

## Rules applying to every task

- Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, `EXECUTION-ORDER.md`, this file, and the current task.
- Inspect the current repository before editing.
- Implement only the current task.
- Preserve Mock, MCP, ChromaDB, existing embeddings, deterministic calculations, and public API compatibility.
- Do not weaken or delete tests.
- Use serialized solution build/test commands.
- Report exact files, commands, test counts, deviations, blockers, and acceptance-criteria status.
- Stop after the current task.
