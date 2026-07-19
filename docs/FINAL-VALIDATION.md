# Final Validation

Phase 8 is the current validation baseline. See [PHASE-8-RESULTS.md](PHASE-8-RESULTS.md) for the recorded Debug, Release, frontend, Playwright, and real-container gate results.

```powershell
docker compose up --build -d
dotnet restore CfoAgent.sln
dotnet build CfoAgent.sln --no-restore --maxcpucount:1
dotnet test CfoAgent.sln --no-build --maxcpucount:1
dotnet test CfoAgent.sln --configuration Release --maxcpucount:1
Set-Location src/cfo-agent-ui
npm ci
npm test -- --run
npm run build
npm run test:e2e:container
```

Run `scripts/test-phase-8-containers.ps1` from the repository root for isolated container resilience validation. `docker compose down` preserves PostgreSQL and ChromaDB volumes; do not use `down -v` unless intentionally clearing local data.
