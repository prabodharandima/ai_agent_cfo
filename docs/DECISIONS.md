# Architecture And Domain Decisions

## Product Boundary

This repository delivers a two-day interview MVP: a CEO-facing CFO AI assistant that answers weekly sales summaries, week-over-week comparisons, current-month top products, five-year sales forecasts, and annual-target or assumption questions.

## Architecture

- The business application is one ASP.NET Core `net10.0` monolith named `CfoAgent.Api` when it is created.
- The CFO Orchestrator, Sales Analysis, Forecasting, and Financial Knowledge agents run as focused in-process classes. They are not microservices and do not call one another over HTTP.
- No separate application, domain, infrastructure, or per-agent class-library projects are permitted.
- MCP is limited to controlled integration adapters: one read-only Finance MCP server and one restricted knowledge-files connection. MCP is neither RAG nor a replacement architecture for the monolith.

## Data And Calculations

- SQLite is authoritative for structured sales and budget data.
- ChromaDB is reserved for chunked financial Markdown knowledge documents; sales transactions do not belong in ChromaDB.
- Revenue, profit, comparisons, rankings, dates, percentages, and forecasts are calculated deterministically in C# and/or SQL. An LLM never invents or calculates financial values.
- Time-dependent finance behavior uses an injected `TimeProvider`, a documented Monday-to-Sunday business week, and a configurable demo date.

## AI And Safety

- This MVP uses only a deterministic offline Mock LLM through `Microsoft.Extensions.AI.IChatClient`.
- Real LLM providers, credentials, web search, autonomous planning loops, reflection agents, and long-term chat memory are out of scope.
- User prompts are untrusted. Tools must be allow-listed, filesystem access restricted to `data/knowledge`, and arbitrary SQL, shell execution, and write-capable MCP tools are prohibited.

## Scope And Quality

- The frontend will be a single React and TypeScript page; the backend will be ASP.NET Core Web API.
- Implementation proceeds one task at a time, with deterministic tests added and run after each phase.
- Authentication, message brokers, CQRS, generic repositories, unnecessary abstractions, modular-monolith patterns, and microservices are out of scope.
