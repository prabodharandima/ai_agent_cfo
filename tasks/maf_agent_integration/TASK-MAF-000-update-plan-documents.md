# TASK-MAF-000 — Update Planning Documents Before Coding

## Goal

Record the planned Microsoft Agent Framework integration without describing unimplemented behavior as current architecture.

## Read first

- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `IMPLEMENTATION-PLAN.md`
- repository execution/instruction documents
- `tasks/maf_agent_integration/README.md`

## Required changes

Update only planning-oriented documentation:

- `IMPLEMENTATION-PLAN.md`
- `tasks/maf_agent_integration/README.md`
- `AGENT.md` only if execution guidance is missing

Document:

- planned Microsoft Agent Framework features;
- task order;
- package/API version uncertainty;
- compatibility risks;
- deterministic boundaries that must remain unchanged;
- requirement to update architecture only after verified implementation;
- requirement to create one result file per task;
- one Codex session and one commit per task.

Use future-state wording such as:

> Planned enhancement

Do not claim that middleware, streaming, sessions, context providers, or telemetry are already implemented.

## Do not modify

- application code;
- tests;
- package references;
- `APPLICATION_ARCHITECTURE.md`, except to add a clearly labeled future-work note only if absolutely necessary.

## Validation

Run:

```bash
git diff --check
```

## Result file

Create:

`tasks/maf_agent_integration/TASK-MAF-000-RESULT.md`

Include only:

- files changed;
- planned features recorded;
- constraints recorded;
- blockers;
- whether the task is complete.
