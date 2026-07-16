using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Data;
using CfoAgent.Api.Data.Seed;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Health;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Ingestion;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var seedRequested = args.Contains("--seed", StringComparer.OrdinalIgnoreCase);
var ragIngestionRequested = args.Contains("--ingest-rag", StringComparer.OrdinalIgnoreCase);

builder.Services.AddOptions<ApplicationOptions>()
    .BindConfiguration(ApplicationOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "Application:Name is required.")
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Database:ConnectionString is required.")
    .ValidateOnStart();

builder.Services.AddOptions<FinanceOptions>()
    .BindConfiguration(FinanceOptions.SectionName)
    .Validate(options => options.DemoDate != default, "Finance:DemoDate is required.")
    .ValidateOnStart();

builder.Services.AddOptions<ChromaOptions>()
    .BindConfiguration(ChromaOptions.SectionName)
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Chroma:BaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CollectionName), "Chroma:CollectionName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Tenant), "Chroma:Tenant is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Database), "Chroma:Database is required.")
    .Validate(options => options.TimeoutSeconds > 0, "Chroma:TimeoutSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<RagOptions>()
    .BindConfiguration(RagOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.KnowledgeFilesRoot), "Rag:KnowledgeFilesRoot is required.")
    .Validate(options => options.MaxChunkCharacters >= 256, "Rag:MaxChunkCharacters must be at least 256.")
    .Validate(options => options.MaxKnowledgeContextCharacters >= 256, "Rag:MaxKnowledgeContextCharacters must be at least 256.")
    .Validate(options => options.MaximumRetrievalDistance >= 0, "Rag:MaximumRetrievalDistance must not be negative.")
    .ValidateOnStart();

builder.Services.AddOptions<AiOptions>()
    .BindConfiguration(AiOptions.SectionName)
    .Validate(options => string.Equals(options.Provider, "Mock", StringComparison.Ordinal), "AI:Provider must be Mock for this MVP.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "AI:Model is required.")
    .Validate(options => options.SimulatedDelayMilliseconds >= 0, "AI:SimulatedDelayMilliseconds must not be negative.")
    .ValidateOnStart();

builder.Services.AddOptions<McpOptions>()
    .BindConfiguration(McpOptions.SectionName)
    .Validate(options => options.Finance.TimeoutSeconds > 0, "Mcp:Finance:TimeoutSeconds must be greater than zero.")
    .Validate(options => !options.Finance.Enabled || !string.IsNullOrWhiteSpace(options.Finance.ServerProjectPath), "Mcp:Finance:ServerProjectPath is required when Finance MCP is enabled.")
    .Validate(options => options.KnowledgeFiles.TimeoutSeconds > 0, "Mcp:KnowledgeFiles:TimeoutSeconds must be greater than zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.KnowledgeFiles.RootPath), "Mcp:KnowledgeFiles:RootPath is required.")
    .ValidateOnStart();

builder.Services.AddOptions<FrontendOptions>()
    .BindConfiguration(FrontendOptions.SectionName)
    .Validate(options => Uri.TryCreate(options.AllowedOrigin, UriKind.Absolute, out _), "Frontend:AllowedOrigin must be an absolute URI.")
    .ValidateOnStart();

var frontendOptions = builder.Configuration.GetRequiredSection(FrontendOptions.SectionName).Get<FrontendOptions>()
    ?? throw new InvalidOperationException("Frontend configuration is required.");
var databaseOptions = builder.Configuration.GetRequiredSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Database configuration is required.");

builder.Services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(databaseOptions.ConnectionString));
builder.Services.AddSingleton<TimeProvider, DemoTimeProvider>();
builder.Services.AddScoped<DevelopmentDatabaseInitializer>();
builder.Services.AddScoped<DevelopmentFinanceSeeder>();
builder.Services.AddScoped<SalesAnalysisService>();
builder.Services.AddScoped<SalesForecastingService>();
builder.Services.AddScoped<SalesAnalysisAgent>();
builder.Services.AddScoped<ForecastingAgent>();
builder.Services.AddScoped<FinancialKnowledgeAgent>();
builder.Services.AddScoped<CfoOrchestratorAgent>();
builder.Services.AddSingleton<IChatClient, MockChatClient>();
builder.Services.AddSingleton<CfoAgentFramework>();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, DeterministicTokenHashEmbeddingGenerator>();
builder.Services.AddScoped<RagDocumentIngestionService>();
builder.Services.AddScoped<FinancialKnowledgeRetrievalService>();
builder.Services.AddSingleton<FinanceMcpClient>();
builder.Services.AddSingleton<IFinanceMcpClient>(services => services.GetRequiredService<FinanceMcpClient>());
builder.Services.AddSingleton<KnowledgeFileMcpClient>();
builder.Services.AddSingleton<KnowledgeFileMcpProcessClient>();
builder.Services.AddSingleton<IKnowledgeFileMcpProcessClient>(services => services.GetRequiredService<KnowledgeFileMcpProcessClient>());
builder.Services.AddSingleton<IKnowledgeFileMcpClient, KnowledgeFileMcpAccess>();
builder.Services.AddSingleton<FinanceMcpFallback>();
builder.Services.AddSingleton<KnowledgeFileMcpFallback>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
    {
        policy.WithOrigins(frontendOptions.AllowedOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

builder.Services.AddHttpClient<ChromaHealthCheck>((serviceProvider, client) =>
{
    var chroma = serviceProvider.GetRequiredService<IOptions<ChromaOptions>>().Value;
    client.BaseAddress = new Uri(chroma.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(chroma.TimeoutSeconds);
});

builder.Services.AddHttpClient<ChromaClient>((serviceProvider, client) =>
{
    var chroma = serviceProvider.GetRequiredService<IOptions<ChromaOptions>>().Value;
    client.BaseAddress = new Uri(chroma.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(chroma.TimeoutSeconds);
});

builder.Services.AddHealthChecks()
    .AddCheck<ChromaHealthCheck>("chroma", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseInitializer>();
    await databaseInitializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

if (seedRequested)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("The seed command is only available in the Development environment.");
    }

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentFinanceSeeder>();
    await seeder.SeedAsync(app.Lifetime.ApplicationStopping);
    return;
}

if (ragIngestionRequested)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("The RAG ingestion command is only available in the Development environment.");
    }

    using var scope = app.Services.CreateScope();
    var ingestionService = scope.ServiceProvider.GetRequiredService<RagDocumentIngestionService>();
    var result = await ingestionService.IngestAsync(app.Lifetime.ApplicationStopping);
    Console.WriteLine($"RAG ingestion: documents={result.Documents}, chunksAddedOrUpdated={result.ChunksAddedOrUpdated}, skipped={result.Skipped}, failed={result.Failed}");

    foreach (var failure in result.Failures)
    {
        Console.Error.WriteLine($"RAG ingestion failure: {failure.SourcePath}: {failure.Message}");
    }

    if (result.Failed > 0)
    {
        throw new InvalidOperationException("RAG ingestion completed with document failures.");
    }

    return;
}

app.UseCors("LocalFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", (IOptions<ApplicationOptions> applicationOptions, IOptions<AiOptions> aiOptions) =>
{
    var application = applicationOptions.Value;
    var ai = aiOptions.Value;

    return Results.Ok(new
    {
        application = application.Name,
        demoMode = application.DemoMode,
        aiProvider = ai.Provider,
        model = ai.Model
    });
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

app.Run();

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var dependencies = report.Entries.Select(entry => new
    {
        name = entry.Key,
        status = entry.Value.Status.ToString(),
        description = entry.Value.Description
    });

    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        dependencies
    });
}
