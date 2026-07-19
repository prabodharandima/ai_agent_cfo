using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Chat;
using CfoAgent.Api.Health;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Observability;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Ingestion;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var ragIngestionRequested = args.Contains("--ingest-rag", StringComparer.OrdinalIgnoreCase);

builder.Services.AddOptions<ApplicationOptions>()
    .BindConfiguration(ApplicationOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "Application:Name is required.")
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
    .Validate(options => string.Equals(options.Provider, "Mock", StringComparison.OrdinalIgnoreCase)
        || string.Equals(options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase), "AI:Provider must be Mock or Ollama.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "AI:Model is required.")
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)), "AI:BaseUrl must be an absolute HTTP or HTTPS URI.")
    .Validate(options => options.TimeoutSeconds is > 0 and <= 600, "AI:TimeoutSeconds must be between 1 and 600.")
    .Validate(options => double.IsFinite(options.Temperature) && options.Temperature is >= 0 and <= 2, "AI:Temperature must be finite and between 0 and 2.")
    .Validate(options => options.ContextLength is >= 1_024 and <= 32_768, "AI:ContextLength must be between 1024 and 32768.")
    .Validate(options => options.MaxOutputTokens is >= 1 and <= 1_024 && options.MaxOutputTokens < options.ContextLength, "AI:MaxOutputTokens must be between 1 and 1024 and less than AI:ContextLength.")
    .Validate(options => options.SimulatedDelayMilliseconds >= 0, "AI:SimulatedDelayMilliseconds must not be negative.")
    .ValidateOnStart();

builder.Services.AddOptions<McpOptions>()
    .BindConfiguration(McpOptions.SectionName)
    .Validate(options => options.Finance.TimeoutSeconds > 0, "Mcp:Finance:TimeoutSeconds must be greater than zero.")
    .Validate(options => !options.Finance.Enabled || IsAbsoluteHttpUri(options.Finance.BaseUrl), "Mcp:Finance:BaseUrl must be an absolute HTTP or HTTPS URI when Finance MCP is enabled.")
    .Validate(options => options.KnowledgeFiles.TimeoutSeconds > 0, "Mcp:KnowledgeFiles:TimeoutSeconds must be greater than zero.")
    .Validate(options => !options.KnowledgeFiles.Enabled || IsAbsoluteHttpUri(options.KnowledgeFiles.BaseUrl), "Mcp:KnowledgeFiles:BaseUrl must be an absolute HTTP or HTTPS URI when Knowledge File MCP is enabled.")
    .Validate(options => !options.KnowledgeFiles.UseLocalFallback || !string.IsNullOrWhiteSpace(options.KnowledgeFiles.RootPath), "Mcp:KnowledgeFiles:RootPath is required when local fallback is enabled.")
    .Validate(options => !options.KnowledgeFiles.UseLocalFallback || builder.Environment.IsDevelopment(), "Knowledge File MCP local fallback is permitted only in Development.")
    .ValidateOnStart();

builder.Services.AddOptions<FrontendOptions>()
    .BindConfiguration(FrontendOptions.SectionName)
    .Validate(options => Uri.TryCreate(options.AllowedOrigin, UriKind.Absolute, out _), "Frontend:AllowedOrigin must be an absolute URI.")
    .ValidateOnStart();

var frontendOptions = builder.Configuration.GetRequiredSection(FrontendOptions.SectionName).Get<FrontendOptions>()
    ?? throw new InvalidOperationException("Frontend configuration is required.");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SalesForecastingService>();
builder.Services.AddScoped<SalesAnalysisAgent>();
builder.Services.AddScoped<ForecastingAgent>();
builder.Services.AddScoped<FinancialKnowledgeAgent>();
builder.Services.AddScoped<CfoOrchestratorAgent>();
builder.Services.AddHttpClient(AiOptions.OllamaHttpClientName, (serviceProvider, client) =>
{
    var ai = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
    client.BaseAddress = new Uri(ai.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds);
});
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var ai = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;

    if (string.Equals(ai.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
    {
        return ActivatorUtilities.CreateInstance<MockChatClient>(serviceProvider);
    }

    if (string.Equals(ai.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(AiOptions.OllamaHttpClientName);
        var transport = (IChatClient)new OllamaApiClient(httpClient, ai.Model);
        return new OllamaChatClient(
            transport,
            ai,
            serviceProvider.GetRequiredService<ILogger<OllamaChatClient>>());
    }

    throw new InvalidOperationException("AI provider configuration was not validated.");
});
builder.Services.AddSingleton<CfoAgentFramework>();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, DeterministicTokenHashEmbeddingGenerator>();
builder.Services.AddScoped<RagDocumentIngestionService>();
builder.Services.AddScoped<FinancialKnowledgeRetrievalService>();
builder.Services.AddHttpClient(FinanceMcpClient.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient(KnowledgeFileMcpHttpClient.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<FinanceMcpClient>();
builder.Services.AddSingleton<IFinanceMcpClient>(serviceProvider => serviceProvider.GetRequiredService<FinanceMcpClient>());
builder.Services.AddSingleton<IFinanceMcpRemoteClient>(serviceProvider => serviceProvider.GetRequiredService<FinanceMcpClient>());
builder.Services.AddSingleton<KnowledgeFileMcpClient>();
builder.Services.AddSingleton<IKnowledgeFileMcpRemoteClient, KnowledgeFileMcpHttpClient>();
builder.Services.AddSingleton<IKnowledgeFileMcpClient, KnowledgeFileMcpAccess>();
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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("chat", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

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
    .AddCheck<ChromaHealthCheck>("chroma", tags: ["ready"])
    .AddCheck<McpConfigurationHealthCheck>("mcp", tags: ["ready"])
    .AddCheck<OllamaHealthCheck>("ollama", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseExceptionHandler();

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
app.UseRateLimiter();

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

app.MapChatEndpoints();

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

static bool IsAbsoluteHttpUri(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri)
    && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

public partial class Program;
