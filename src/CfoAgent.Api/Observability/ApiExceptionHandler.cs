using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Observability;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger,
    IOptions<AiOptions> aiOptions) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            OllamaProviderException { FailureKind: OllamaFailureKind.Timeout } => (StatusCodes.Status504GatewayTimeout, "The selected model provider timed out."),
            OllamaProviderException { FailureKind: OllamaFailureKind.Unavailable } => (StatusCodes.Status503ServiceUnavailable, "The selected model provider is temporarily unavailable."),
            OllamaProviderException => (StatusCodes.Status503ServiceUnavailable, "The selected model provider returned an unusable response."),
            ChromaDependencyException => (StatusCodes.Status503ServiceUnavailable, "A required dependency is temporarily unavailable."),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            OperationCanceledException when !httpContext.RequestAborted.IsCancellationRequested => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "The requested operation is temporarily unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
        };

        if (exception is OllamaProviderException providerException)
        {
            logger.LogWarning(
                "Ollama operation failed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; Outcome: {Outcome}; FailureCategory: {FailureCategory}; StatusCode: {StatusCode}",
                "Ollama",
                aiOptions.Value.Model,
                "chat",
                "Failure",
                providerException.FailureKind,
                statusCode);
        }
        else
        {
            logger.LogError(
                "Unhandled request failure. CorrelationId: {CorrelationId}; FailureType: {FailureType}; StatusCode: {StatusCode}",
                httpContext.TraceIdentifier,
                exception.GetType().Name,
                statusCode);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
        httpContext.Response.StatusCode = statusCode;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
