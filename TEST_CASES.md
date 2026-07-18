# Manual Test Cases

This guide is for manually exercising the CFO AI Agent MVP. It uses the deterministic demo data, Mock LLM, local SQLite database, and local ChromaDB container. No LLM API key or cloud account is required.

## Prerequisites

Before starting, confirm that the following are available:

- Docker Desktop is running.
- .NET SDK `10.0.302` (or the compatible patch selected by [global.json](global.json)) is installed.
- Node.js 22 or later and npm are installed.
- Ports `5173`, `5260`, and `8000` are free, or any older local instances of the UI, API, and ChromaDB have been stopped.

The default configuration intentionally keeps Finance MCP and Knowledge File MCP disabled. The manual tests below therefore exercise the supported local deterministic finance and ChromaDB RAG paths.

## Start The Application

Open PowerShell at the repository root and run the following commands in order:

```powershell
docker compose up -d
dotnet restore CfoAgent.sln
dotnet run --project src/CfoAgent.Api -- --seed
dotnet run --project src/CfoAgent.Api -- --ingest-rag
dotnet run --project src/CfoAgent.Api
```

Keep the last command running. In a second PowerShell window, start the frontend:

```powershell
Set-Location src/cfo-agent-ui
npm ci
npm run dev
```

Open `http://localhost:5173` in a browser. The API is available at `http://localhost:5260`.

### Pre-Test Checks

1. Open `http://localhost:5260/health/live`.
2. Open `http://localhost:5260/health/ready`.
3. Confirm that both return a successful response. The readiness response should list SQLite, ChromaDB, and MCP configuration dependencies. MCP being disabled by default is expected and does not make the application unavailable.
4. Open `http://localhost:5173` and confirm the page shows `CFO AI Agent`, a `Mock LLM` badge, five example prompt buttons, and a message composer.

If ChromaDB was stopped with `docker compose down -v`, run the seed and ingestion commands again before executing the knowledge test case.

## Manual Test Cases

### TC-01: Application Starts And Is Ready

**Steps**

1. Complete the startup and pre-test checks above.
2. Open `http://localhost:5260/` in a browser.
3. Open `http://localhost:5260/health/ready`.

**Expected behavior**

- The root API response identifies `CFO AI Agent`, `Mock` as the provider, and `DeterministicMock` as the model.
- The readiness endpoint returns an overall healthy status with SQLite and ChromaDB reachable.
- No external LLM credential is requested or required.

### TC-02: Weekly Sales Summary

**Steps**

1. In the UI, enter `Give me the sales summary of this week.`
2. Select **Send** or press Enter.

**Expected behavior**

- A CFO AI Agent response appears with a Sales Analysis Agent label.
- The response contains a current-week period and structured sales KPIs.
- The result is deterministic for the configured demo date and does not claim that a real LLM calculated the values.

### TC-03: Week-Over-Week Comparison

**Steps**

1. Enter `Compare this week's sales with last week.`
2. Submit the prompt.

**Expected behavior**

- The response contains current and previous periods.
- It presents a deterministic comparison and direction/change information.
- The result remains available with MCP disabled because the local finance service is the configured fallback path.

### TC-04: Current-Month Top Products

**Steps**

1. Enter `Show me the top five products this month.`
2. Submit the prompt.

**Expected behavior**

- A top-products response is displayed with a structured product table.
- The table contains up to five products for the current demo month.
- Product values are presented as finance data, not as a free-form estimate.

### TC-05: Five-Year Forecast

**Steps**

1. Enter `Give me the sales forecast for the next five years.`
2. Submit the prompt.

**Expected behavior**

- A Forecasting Agent response appears.
- A five-year forecast table and chart are displayed.
- The response includes assumptions or warnings when applicable.
- Forecast values are deterministic calculations from historical finance data; the Mock LLM is not used to calculate them.

### TC-06: Annual Target With Knowledge Sources

**Steps**

1. Enter `What is the annual sales target and what assumptions were used?`
2. Submit the prompt.

**Expected behavior**

- A Financial Knowledge Agent response appears.
- The answer includes target/assumption information and one or more source citations.
- The citations identify indexed documents from `data/knowledge`.
- The response is grounded in ChromaDB retrieval rather than raw unrestricted file access.

### TC-07: Empty Prompt Validation

**Steps**

1. Open or refresh the UI.

2. Leave the message box empty or enter only spaces.
3. Observe the **Send** button.

**Expected behavior**

- The **Send** button remains disabled for an empty or whitespace-only prompt.
- No API request is sent and no assistant response is created.

### TC-08: Unsupported Question Is Controlled

**Steps**

1. Enter `Write a marketing slogan for a new shoe brand.`
2. Submit the prompt.

**Expected behavior**

- The application returns a controlled response explaining that the request is outside the CFO MVP scope.
- It does not invent finance values, call a real model provider, or expose internal errors.

### TC-09: API Request Validation

**Steps**

1. With the API running, execute this in PowerShell:

   ```powershell
   curl.exe -i -X POST http://localhost:5260/api/chat -H 'Content-Type: application/json' -d '{"message":"   "}'
   ```

2. Inspect the returned error response.

**Expected behavior**

- The API returns HTTP `400` with ASP.NET Core validation problem details.
- The response identifies `message` as required and does not expose a stack trace, SQL, prompts, or internal file paths.

### TC-10: Graceful RAG Dependency Failure

**Steps**

1. Stop only the ChromaDB container:

   ```powershell
   docker compose stop chromadb
   ```

2. In the UI, submit the annual target and assumptions prompt from TC-06.
3. Restart ChromaDB when finished:

   ```powershell
   docker compose up -d
   ```

**Expected behavior**

- The application shows a clear, controlled unavailable/error message for the knowledge request.
- It does not display a stack trace, ChromaDB connection details, or sensitive configuration values.
- After ChromaDB restarts, rerun the ingestion command if needed and TC-06 succeeds again.

## Shut Down

Stop the API and frontend with `Ctrl+C` in their respective terminals. To stop ChromaDB while retaining its local data, run:

```powershell
docker compose down
```

Use `docker compose down -v` only when a fresh ChromaDB volume is required; it removes the local vector data and requires RAG ingestion again.
