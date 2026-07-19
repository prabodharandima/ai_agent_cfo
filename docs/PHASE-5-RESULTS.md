# Phase 5 Results

> Historical record: the described local SQLite/stdio E2E setup was superseded in Phase 8. See [PHASE-8-RESULTS.md](PHASE-8-RESULTS.md).

## Scope

Phase 5 exposes the deterministic CFO orchestrator through `POST /api/chat` and provides a single React TypeScript chat page for the five MVP scenarios.

## User journey coverage

Playwright starts the local ChromaDB dependency, seeds SQLite, ingests the Markdown knowledge documents into ChromaDB, and launches the API with the two existing MCP integrations enabled. The browser tests cover:

- Weekly sales summary with KPI cards and the Sales Analysis Agent label.
- Week-over-week comparison and deterministic direction values.
- Current-month top-products table.
- Five-year deterministic forecast table and chart.
- Annual target and assumptions with RAG source citations.
- Empty-prompt prevention and an API dependency failure message.

The E2E startup script uses the configured fixed demo date. MCP servers remain the existing lazy stdio integrations: the API launches them only when the relevant browser scenario first requires a tool.

## Diagnostics

Playwright retains traces and screenshots only when a browser test fails. This keeps normal runs clean while preserving actionable browser diagnostics.

## Validation results

- `docker compose up -d`: ChromaDB started successfully.
- `dotnet test CfoAgent.sln --configuration Release --maxcpucount:1`: 116 backend tests passed.
- `npm test -- --run`: 10 frontend unit tests passed.
- `npm run test:e2e`: 7 Chromium browser scenarios passed.
- Serialized Debug solution build: passed with zero warnings and zero errors.
- Serialized Debug backend tests: 116 tests passed.

## Gate

TASK-CFO-021 is complete. The five deterministic CFO browser scenarios, invalid-prompt prevention, dependency failure message, backend regression suite, and frontend regression suite all pass locally.
