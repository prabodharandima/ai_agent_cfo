using System.Text.Json;
using System.Text.Json.Nodes;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;

namespace CfoAgent.Api.Features.Chat;

public static class ChatEndpoints
{
    public const int MaximumMessageLength = 4_000;
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", HandleAsync)
            .WithName("PostChat")
            .WithSummary("Ask the CFO assistant a supported finance question.")
            .WithDescription("Returns a deterministic CFO response for one of the five MVP scenarios.")
            .Produces<ChatResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("chat")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                var requestBody = operation.RequestBody;
                if (requestBody?.Content is { } content
                    && content.TryGetValue("application/json", out var mediaType)
                    && mediaType is not null)
                {
                    mediaType.Examples = CreateExamples();
                }

                return Task.CompletedTask;
            });

        endpoints.MapPost("/api/chat/stream", HandleStreamAsync)
            .WithName("PostStreamingChat")
            .WithSummary("Stream a CFO assistant response using server-sent events.")
            .WithDescription("Emits safe progress, answer-content, and completion events. POST /api/chat remains the default endpoint.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("chat");

        return endpoints;
    }

    private static async Task<Results<Ok<ChatResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        ChatRequest? request,
        CfoOrchestratorAgent orchestrator,
        AiProviderDescriptor aiProvider,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var conversationId = string.IsNullOrWhiteSpace(request!.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId.Trim();
        var logger = loggerFactory.CreateLogger("CfoAgent.Api.Features.Chat");

        logger.LogInformation(
            "Processing CFO chat request. ConversationIdProvided: {ConversationIdProvided}; MessageLength: {MessageLength}",
            !string.IsNullOrWhiteSpace(request.ConversationId),
            request.Message!.Length);

        var result = await orchestrator.HandleAsync(
            new AgentRequest(request.Message),
            httpContext.RequestAborted);
        var model = new ChatModel(aiProvider.ProviderName, aiProvider.ModelName);

        return TypedResults.Ok(ChatResponse.FromAgentResult(result, conversationId, model));
    }

    private static async Task HandleStreamAsync(
        ChatRequest? request,
        CfoOrchestratorAgent orchestrator,
        AiProviderDescriptor aiProvider,
        ILoggerFactory loggerFactory,
        HttpContext httpContext)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            await TypedResults.ValidationProblem(errors).ExecuteAsync(httpContext);
            return;
        }

        var cancellationToken = httpContext.RequestAborted;
        var conversationId = string.IsNullOrWhiteSpace(request!.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId.Trim();
        var logger = loggerFactory.CreateLogger("CfoAgent.Api.Features.Chat");

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await WriteEventAsync(httpContext.Response, "progress", new ChatStreamProgress("classifying"), cancellationToken);
            var agentRequest = new AgentRequest(request.Message!);
            var intent = await orchestrator.ClassifyAsync(agentRequest.Message, cancellationToken);

            await WriteEventAsync(httpContext.Response, "progress", new ChatStreamProgress("retrieving"), cancellationToken);
            var result = await orchestrator.HandleClassifiedAsync(agentRequest, intent, cancellationToken);

            await WriteEventAsync(httpContext.Response, "progress", new ChatStreamProgress("generating"), cancellationToken);
            await WriteAnswerContentAsync(httpContext.Response, result.Answer, cancellationToken);

            var response = ChatResponse.FromAgentResult(
                result,
                conversationId,
                new ChatModel(aiProvider.ProviderName, aiProvider.ModelName));
            await WriteEventAsync(httpContext.Response, "progress", new ChatStreamProgress("completed"), cancellationToken);
            await WriteEventAsync(
                httpContext.Response,
                "completed",
                new ChatStreamCompletion(
                    response.ConversationId,
                    response.AgentNames,
                    response.ResponseType,
                    response.Sources,
                    response.Assumptions,
                    response.Warnings,
                    response.DataPeriod,
                    response.Model),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("CFO streaming chat request cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            var error = ToStreamError(exception);
            logger.LogWarning(
                "CFO streaming chat request failed. FailureType: {FailureType}; StatusCode: {StatusCode}",
                exception.GetType().Name,
                error.Status);
            await WriteEventAsync(httpContext.Response, "error", error, cancellationToken);
        }
    }

    private static async Task WriteAnswerContentAsync(HttpResponse response, string answer, CancellationToken cancellationToken)
    {
        const int maximumChunkLength = 256;
        for (var index = 0; index < answer.Length; index += maximumChunkLength)
        {
            var length = Math.Min(maximumChunkLength, answer.Length - index);
            await WriteEventAsync(response, "content", new ChatStreamContent(answer.Substring(index, length)), cancellationToken);
        }
    }

    private static async Task WriteEventAsync<T>(HttpResponse response, string eventName, T payload, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await response.WriteAsync("data: ", cancellationToken);
        await JsonSerializer.SerializeAsync(response.Body, payload, StreamJsonOptions, cancellationToken);
        await response.WriteAsync("\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static ChatStreamError ToStreamError(Exception exception) => exception switch
    {
        PromptInjectionRiskException => new ChatStreamError(StatusCodes.Status400BadRequest, "The request contains unsupported instruction content."),
        AiProviderException { FailureKind: AiProviderFailureKind.Timeout } => new ChatStreamError(StatusCodes.Status504GatewayTimeout, "The selected model provider timed out."),
        AiProviderException => new ChatStreamError(StatusCodes.Status503ServiceUnavailable, "The selected model provider is temporarily unavailable."),
        McpDependencyException or VectorSearchDependencyException => new ChatStreamError(StatusCodes.Status503ServiceUnavailable, "A required dependency is temporarily unavailable."),
        TimeoutException => new ChatStreamError(StatusCodes.Status504GatewayTimeout, "The request timed out."),
        _ => new ChatStreamError(StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
    };

    private static Dictionary<string, string[]> Validate(ChatRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request is null)
        {
            errors["request"] = ["A request body is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors["message"] = ["Message is required and cannot be blank."];
        }
        else if (request.Message.Length > MaximumMessageLength)
        {
            errors["message"] = [$"Message must not exceed {MaximumMessageLength} characters."];
        }

        return errors;
    }

    private static Dictionary<string, IOpenApiExample> CreateExamples() => new(StringComparer.Ordinal)
    {
        ["weeklySalesSummary"] = Example("Give me the sales summary of this week."),
        ["weekOverWeekComparison"] = Example("Compare this week's sales with last week."),
        ["topProducts"] = Example("Show me the top five products this month."),
        ["fiveYearForecast"] = Example("Give me the sales forecast for the next five years."),
        ["annualTargetAndAssumptions"] = Example("What is the annual sales target and what assumptions were used?")
    };

    private static OpenApiExample Example(string message) => new()
    {
        Value = JsonNode.Parse($$"""{"message":"{{message}}"}""")
    };
}
