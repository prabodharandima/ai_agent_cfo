# TASK-MAF-004 — Streaming Responses

## Goal

Add optional streaming using Microsoft Agent Framework streaming APIs without breaking the existing chat endpoint.

## Before editing

Read repository instructions, the integration README, all earlier result files, Git history, and current code.

## Requirements

- Keep existing `POST /api/chat` unchanged.
- Add one separate streaming endpoint.
- Prefer SSE unless the repository already uses another standard.
- Stream safe progress and answer content.
- Preserve request cancellation.
- Send structured completion metadata at the end.
- Keep non-streaming as the default.

Allowed progress stages:

- classifying;
- retrieving;
- generating;
- completed.

Do not stream system prompts, raw RAG context, secrets, internal errors, stack traces, or raw MCP responses.

## Tests

Cover:

- successful stream;
- event order;
- completion payload;
- cancellation;
- safe error handling;
- unchanged existing endpoint.

Run focused tests, build, full tests, and `git diff --check`.

## Documentation

After tests pass, update the current-state endpoint and request-flow documentation.

## Result file

Create `TASK-MAF-004-RESULT.md`.
