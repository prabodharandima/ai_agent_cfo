# Codex Global Instructions

Apply these rules to every task.

1. Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, and the current task file before changing code.
2. Inspect the current repository; do not assume previous tasks were implemented perfectly.
3. Implement only the current task.
4. Keep the backend as one ASP.NET Core monolith project.
5. Prefer straightforward classes and interfaces over extra layers.
6. Use async APIs and pass `CancellationToken`.
7. Inject `TimeProvider` wherever “today,” “this week,” or “this month” is calculated.
8. Do not let an LLM calculate totals, percentages, forecasts, dates, or rankings.
9. Do not call any real LLM provider.
10. Do not store secrets in source control.
11. Add or update tests for behavior introduced by the task.
12. Run all validation commands named by the task.
13. Fix failures caused by the current work.
14. Do not silently weaken or delete existing tests.
15. At completion, report:
    - files changed,
    - design choices,
    - validation commands and outcomes,
    - remaining limitations,
    - whether the phase gate is satisfied.
