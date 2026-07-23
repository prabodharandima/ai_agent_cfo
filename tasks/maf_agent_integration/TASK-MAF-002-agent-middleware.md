# TASK-MAF-002 — Agent Middleware

## Goal

Add the smallest practical Microsoft Agent Framework middleware integration for cross-cutting agent concerns.

## Before editing

Read:

- repository instructions;
- `tasks/maf_agent_integration/README.md`;
- all earlier task result files;
- current Git log and working tree;
- current implementation.

## Implement

Where supported by the selected framework version, add middleware for:

- execution timing;
- correlation ID propagation;
- safe agent/provider logging;
- simple deterministic prompt-injection risk checks;
- sensitive-output redaction.

## Constraints

- Do not move business rules into middleware.
- Do not let middleware select MCP tools.
- Do not duplicate global API exception handling.
- Do not log full prompts, retrieved context, secrets, raw finance values, or model responses.
- Do not change public API contracts.
- Keep prompt-injection detection configurable and simple.

## Tests

Add focused tests for:

- middleware invocation;
- safe logging metadata;
- suspicious prompt handling;
- output redaction;
- cancellation propagation;
- normal finance requests.

Run focused tests, then:

```bash
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
git diff --check
```

## Documentation

After code and tests pass, update only the relevant current-state sections of:

- `APPLICATION_ARCHITECTURE.md`
- `AGENT.md`
- setup documentation if configuration changed

## Result file

Create `TASK-MAF-002-RESULT.md` with:

- previous task changes detected;
- files changed;
- middleware behavior;
- tests and results;
- documentation updated;
- warnings;
- blockers;
- completion status.
