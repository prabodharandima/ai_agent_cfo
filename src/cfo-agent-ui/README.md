# CFO AI Agent UI

The React and TypeScript chat interface is served by Nginx in the complete Docker deployment. It calls `POST /api/chat` through the same-origin proxy and renders the five supported CFO response types, including KPIs, product tables, forecasts, assumptions, warnings, and ChromaDB citations.

## Local Development

Start the complete backend deployment first from the repository root:

```powershell
docker compose up -d
```

Then run Vite from this directory:

```powershell
npm ci
npm run dev
```

If the Docker frontend is using port `5173`, stop it first with `docker compose stop frontend` or choose another Vite port.

## Validation

```powershell
npm run build
npm test -- --run
npm run test:e2e:container
```

The Playwright command targets the already-running Docker frontend and is deterministic when the API uses the default Mock provider. See the root [README](../../README.md) and [USER-GUIDE.md](../../USER-GUIDE.md) for deployment and provider details.
