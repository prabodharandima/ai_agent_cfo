# TASK-MAF-007 — OpenTelemetry

## Goal

Add safe OpenTelemetry tracing and metrics for agent execution.

## Before editing

Read repository instructions, the integration README, all earlier result files, Git history, and current code.

## Instrument

- chat request;
- intent classification;
- selected specialist;
- LLM duration;
- Finance MCP duration;
- ChromaDB duration;
- composition;
- failures;
- cancellations.

Use only safe attributes:

- correlation ID;
- operation;
- agent;
- provider;
- model;
- outcome;
- duration.

Do not record prompts, responses, raw finance data, RAG content, secrets, or connection strings.

Keep exporters optional.

## Tests

Cover:

- expected activities/spans;
- safe attributes;
- error and cancellation recording;
- no sensitive content;
- unchanged behavior when exporter is disabled.

Run focused tests, build, full tests, and `git diff --check`.

## Documentation

After tests pass, update observability and configuration sections.

## Result file

Create `TASK-MAF-007-RESULT.md`.
