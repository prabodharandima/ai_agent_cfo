# Definition Of Done

A task is complete only when all of the following are true:

- Only the current task's scope is implemented; later-task features are not introduced early.
- The single-monolith architecture and Mock-only LLM rule remain intact.
- Relevant code, configuration, and documentation are clear, focused, and free of secrets.
- Relevant tests are added or updated without weakening existing tests.
- Every validation command named by the current task has been run, and failures caused by the task have been fixed and revalidated.
- Financial behavior, when introduced, is deterministic and does not delegate calculations to an LLM.
- The completion report records changed files, design decisions, validation outcomes, limitations, blockers or deviations, and the applicable phase-gate status.
