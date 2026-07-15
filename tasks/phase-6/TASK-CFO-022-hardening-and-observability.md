# TASK-CFO-022 — Hardening, validation, and observability

## Phase

Phase 6 — Hardening and submission

## Goal

Add the minimum production-minded safeguards expected in a Technical Lead assignment.

## Read first

- `AGENT.md`
- `IMPLEMENTATION-PLAN.md`
- `CODEX-GLOBAL-INSTRUCTIONS.md`
- This task file

## Prerequisites

- Phase 5 gate passed.

## Implementation steps


1. Add centralized exception handling with Problem Details.
2. Add structured logs around:
   - request/correlation ID,
   - chosen intent,
   - participating agents,
   - service/MCP/RAG calls,
   - fallback use,
   - duration and outcome.
3. Do not log full financial documents, prompts containing sensitive data, or raw database rows.
4. Add timeouts and cancellation to Chroma and MCP calls.
5. Add dependency readiness checks for SQLite, Chroma, and configured MCP processes.
6. Validate configuration at startup.
7. Add basic request rate limiting only if it is a few lines using built-in ASP.NET Core support; otherwise document as production work.
8. Add security notes for prompt injection, MCP allow-lists, and source grounding.


## Expected files or areas


- middleware/error handling
- logging and health checks
- `docs/SECURITY-NOTES.md`


## Acceptance criteria


- Failures are controlled and traceable.
- Logs identify routing and dependencies without exposing sensitive content.
- Health endpoints reflect dependency status.
- No unnecessary resilience framework is added.


## Validation commands

```bash
dotnet build CfoAgent.sln
dotnet test CfoAgent.sln
cd src/cfo-agent-ui && npm run build && npm test -- --run
```

## Constraints and non-goals

- Do not add distributed tracing infrastructure, dashboards, authentication, or cloud resources.



## Ready-to-paste Codex prompt

Read `AGENT.md`, `IMPLEMENTATION-PLAN.md`, `CODEX-GLOBAL-INSTRUCTIONS.md`, and this task file. Inspect the repository and implement **TASK-CFO-022 only**. Keep the solution simple and monolithic. Run every validation command in this task, fix issues introduced by your changes, and stop after reporting changed files, design decisions, test results, and remaining limitations.
