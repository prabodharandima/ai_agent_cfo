# CFO AI Agent UI

The React and TypeScript chat interface for the CFO AI Agent MVP. It calls the monolith's `POST /api/chat` endpoint and renders the five supported deterministic CFO response types, including KPIs, product tables, forecasts, assumptions, warnings, and cited sources.

Run it from this directory after starting the API and ChromaDB as described in the root [README](../../README.md):

```powershell
npm ci
npm run dev
```

Validation commands:

```powershell
npm run build
npm test -- --run
npm run test:e2e
```

The UI intentionally contains no model provider, authentication, streaming, or persistence logic. See the root documentation for the full architecture, demo prompts, and final validation record.
