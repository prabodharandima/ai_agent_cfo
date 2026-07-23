# TASK-MAF-005 — Bounded Agent Sessions

## Goal

Add bounded in-memory multi-turn sessions using Microsoft Agent Framework session concepts.

## Before editing

Read repository instructions, the integration README, all earlier result files, Git history, and current code.

## Requirements

- Use the existing conversation ID.
- Keep storage in memory only.
- Add configurable message limit and expiration.
- Preserve current single-turn behavior when no prior session exists.
- Isolate sessions by conversation ID.
- Store only minimal conversational context.
- Do not store secrets, raw retrieved documents, credentials, or authorization state.
- Do not allow session content to control authorization.
- Do not add a database.

## Tests

Cover:

- same-session continuity;
- different-session isolation;
- expiration;
- message limit;
- missing conversation ID;
- cancellation;
- no cross-session leakage.

Run focused tests, build, full tests, and `git diff --check`.

## Documentation

After tests pass, update current-state architecture, configuration, limitations, and privacy notes.

## Result file

Create `TASK-MAF-005-RESULT.md`.
