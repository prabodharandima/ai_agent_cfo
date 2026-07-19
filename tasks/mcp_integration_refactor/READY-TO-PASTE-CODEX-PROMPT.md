# Ready-to-Paste Codex Prompt

Replace `<TASK_FILE>` with the current task filename.

```text
You are working in the root directory of the CFO AI Agent project.

Read completely:

- AGENT.md
- APPLICATION_ARCHITECTURE.md
- IMPLEMENTATION-PLAN.md
- CODEX-GLOBAL-INSTRUCTIONS.md
- EXECUTION-ORDER.md
- tasks/mcp_integration_refactor/README-MCP-INTEGRATION-REFACTOR.md
- tasks/mcp_integration_refactor/MCP-INTEGRATION-REFACTOR-EXECUTION-ORDER.md
- tasks/mcp_integration_refactor/<TASK_FILE>

Inspect the actual repository before making changes.
Verify prerequisites and confirm the task matches the current repository state.

Implement only the current task.

Rules:
- Keep the solution simple and do not over-engineer.
- Prefer the official MCP SDK and existing abstractions.
- Do not add plugin registries, workflow engines, custom protocols, schema engines, or background polling unless explicitly required by the task.
- Preserve public API and MCP contracts.
- Preserve PostgreSQL, ChromaDB, Docker, and ownership boundaries.
- Preserve Mock and Ollama support.
- Preserve cancellation and sanitized dependency failures.
- Do not weaken tests.
- Do not start the next task.

Run all validation required by the task and always run:

dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
git diff --check

Report files created/modified/deleted, architecture changes, discovery behavior, tool-selection behavior, security validation, commands, test totals, warnings/errors, assumptions, deviations, and blockers.

Explicitly state whether every acceptance criterion is complete.
Stop after the current task.
```
