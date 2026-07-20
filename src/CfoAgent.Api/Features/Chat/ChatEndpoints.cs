using System.Text.Json.Nodes;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace CfoAgent.Api.Features.Chat;

public static class ChatEndpoints
{
    public const int MaximumMessageLength = 4_000;

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

        return endpoints;
    }

    private static async Task<Results<Ok<ChatResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        ChatRequest? request,
        CfoOrchestratorAgent orchestrator,
        IOptions<AiOptions> aiOptions,
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
        var model = new ChatModel("Ollama", aiOptions.Value.Model);

        return TypedResults.Ok(ChatResponse.FromAgentResult(result, conversationId, model));
    }

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
