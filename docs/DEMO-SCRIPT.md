# CFO AI Agent Demo Script

This is a 10-15 minute reviewer walkthrough. Start the API, ChromaDB, and UI using the root README before the session.

## 1. Set the frame (1 minute)

Open the chat UI at `http://localhost:5173`. Explain that this is one ASP.NET Core business monolith with a React client, local SQLite demo data, ChromaDB for semantic knowledge retrieval, deterministic C#/SQL finance calculations, and a Mock `IChatClient`. No real model credentials are present or required.

## 2. Weekly sales summary (2 minutes)

Ask: `Give me the sales summary of this week.`

Point out the Sales Analysis Agent label, structured KPIs, the fixed demo period, and warnings or assumptions. Explain that the finance values are calculated locally by deterministic code; the Mock LLM only formats verified context.

## 3. Week-over-week comparison (2 minutes)

Ask: `Compare this week's sales with last week.`

Show the current and previous periods and the deterministic change/direction. Mention that this is the same result contract whether Finance MCP is enabled or the local fallback is selected.

## 4. Current-month top products (2 minutes)

Ask: `Show me the top five products this month.`

Show the product table and the Sales Analysis Agent label. This demonstrates structured financial output rather than a free-form calculation by a model.

## 5. Five-year forecast (2 minutes)

Ask: `Give me the sales forecast for the next five years.`

Show the forecast table and chart. State plainly that historical data may come through the optional Finance MCP tool, but the forecast arithmetic always stays in deterministic C# and is never delegated to the Mock LLM.

## 6. Target and assumptions (2 minutes)

Ask: `What is the annual sales target and what assumptions were used?`

Show the Financial Knowledge Agent label, assumptions, and source citations. Explain that ChromaDB performs semantic retrieval and citations. The optional restricted Knowledge File MCP can only list/read files below `data/knowledge`; it does not replace ChromaDB retrieval.

## 7. Reliability and boundaries (2-3 minutes)

Open [README.md](../README.md) and briefly show the Mermaid architecture. Explain that the optional Finance and Knowledge File MCP processes are disabled by default, start lazily, validate their allow-listed tools, use timeout/cancellation, and fall back to local paths on configured dependency failure. They are tool providers rather than business microservices.

Finish by showing [docs/FINAL-VALIDATION.md](FINAL-VALIDATION.md), [docs/TRADE-OFFS.md](TRADE-OFFS.md), and [docs/SECURITY-NOTES.md](SECURITY-NOTES.md). The regression suite covers backend, React unit, and browser scenarios.
