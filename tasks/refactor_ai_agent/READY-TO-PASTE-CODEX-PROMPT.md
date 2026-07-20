# Reusable Codex Prompt

Replace `<TASK_FILE>` with the exact task filename.

```text
You are working in the root directory of the CFO AI Agent repository.

Execute only:

tasks/refactor_ai_agent/<TASK_FILE>

Read completely:

- AGENT.md
- APPLICATION_ARCHITECTURE.md
- IMPLEMENTATION-PLAN.md
- CODEX-GLOBAL-INSTRUCTIONS.md
- EXECUTION-ORDER.md
- tasks/refactor_ai_agent/README-REFACTOR-AI-AGENT.md
- tasks/refactor_ai_agent/REFACTOR-AI-AGENT-EXECUTION-ORDER.md
- tasks/refactor_ai_agent/<TASK_FILE>

Inspect the actual repository before making changes.

Limit scope to CfoAgent.Api and its directly related tests. Do not refactor MCP server projects or the UI.

Implement only the current task.

Key rules:

- Keep the design simple.
- Preserve current behavior and public API contracts.
- Use one clear Orchestrator–Worker flow.
- Apply Clean/Hexagonal boundaries only where they provide real value.
- Do not introduce mediator frameworks, event buses, workflow engines, plugin registries, policy engines, custom agent frameworks, or unnecessary factories.
- Do not create abstractions for theoretical future use.
- Prefer removing accidental complexity over replacing it with different complexity.
- Preserve LLM, MCP, vector-store, cancellation, error handling, and deterministic calculation behavior.
- Do not weaken tests.
- Do not start the next task.

Run all validation required by the task.

Always run:

dotnet restore CfoAgent.sln --maxcpucount:1
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
git diff --check

Report:

- files created
- files modified
- files deleted
- abstractions added
- abstractions removed
- architecture changes
- behavior preserved
- commands executed
- focused test results
- total test counts
- warnings/errors
- assumptions
- deviations
- blockers

Explicitly state whether every acceptance criterion is complete.

Stop after the current task.
```
