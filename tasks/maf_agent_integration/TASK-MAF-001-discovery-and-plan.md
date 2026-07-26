# TASK-MAF-001 — Discovery and Technical Plan

## Goal

Inspect the repository and create a minimal implementation plan for the selected Microsoft Agent Framework features.

## Before editing

Read:

- repository instruction documents;
- `tasks/maf_agent_integration/README.md`;
- `TASK-MAF-000-RESULT.md`;
- current Git history and working tree;
- current source, tests, configuration, and package references.

## Identify

- current `IChatClient` usage;
- orchestrator and specialist-agent boundaries;
- response composition;
- error handling;
- RAG retrieval and context building;
- conversation ID behavior;
- current telemetry;
- package versions already used;
- exact Microsoft Agent Framework packages/APIs compatible with the current target framework.

## Required output

Create:

`tasks/maf_agent_integration/TASK-MAF-001-RESULT.md`

Include:

- current architecture summary;
- package/API choices;
- exact files likely to change per later task;
- compatibility risks;
- recommended implementation order;
- deterministic behavior that must remain unchanged;
- focused tests required per task;
- documentation sections to update after each implementation task.

## Constraints

- Do not modify application code.
- Do not add packages.
- Do not rewrite architecture documentation.
- Do not replace typed Finance MCP operations with unrestricted LLM tool selection.
- Do not run the full test suite.

## Validation

Run only lightweight inspection commands and:

```bash
git diff --check
```
