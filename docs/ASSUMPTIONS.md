# MVP Assumptions

- The project is a local, two-day interview MVP focused on five CEO finance questions rather than a production accounting system.
- The backend will remain one ASP.NET Core `net10.0` monolith. The four required agents remain in process.
- The only AI provider is a deterministic offline Mock LLM. No API key, cloud model, or real provider is required or permitted.
- Financial metrics and forecasts are deterministic C# or SQL outputs. The Mock LLM is limited to classification and executive wording around verified data.
- SQLite will hold structured finance data and ChromaDB will hold only indexed Markdown knowledge documents.
- The local developer environment provides the .NET 10 SDK. Docker, Node.js, and npm will be needed only by later tasks that introduce ChromaDB and the React frontend.
- Scope remains intentionally small: no authentication, user management, microservices, distributed systems, advanced forecasting, or production deployment are included.
