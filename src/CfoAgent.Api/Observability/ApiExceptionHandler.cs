using CfoAgent.Api.AI;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CfoAgent.Api.Observability;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger,
    AiProviderDescriptor aiProvider) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            AiProviderException { FailureKind: AiProviderFailureKind.Timeout } => (StatusCodes.Status504GatewayTimeout, "The selected model provider timed out."),
            AiProviderException { FailureKind: AiProviderFailureKind.Unavailable } => (StatusCodes.Status503ServiceUnavailable, "The selected model provider is temporarily unavailable."),
            AiProviderException => (StatusCodes.Status503ServiceUnavailable, "The selected model provider returned an unusable response."),
            VectorSearchDependencyException => (StatusCodes.Status503ServiceUnavailable, "A required dependency is temporarily unavailable."),
            McpDependencyException => (StatusCodes.Status503ServiceUnavailable, "A required dependency is temporarily unavailable."),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            OperationCanceledException when !httpContext.RequestAborted.IsCancellationRequested => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "CFO assistant is temporarily unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
        };

        if (exception is AiProviderException providerException)
        {
            logger.LogWarning(
                "AI provider operation failed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; Outcome: {Outcome}; FailureCategory: {FailureCategory}; StatusCode: {StatusCode}",
                providerException.ProviderName,
                aiProvider.ModelName,
                "chat",
                "Failure",
                providerException.FailureKind,
                statusCode);
        }
        else if (exception is McpDependencyException dependencyException)
        {
            logger.LogWarning(
                "MCP dependency operation failed. Dependency: {Dependency}; FailureCategory: {FailureCategory}; StatusCode: {StatusCode}",
                dependencyException.DependencyName,
                dependencyException.FailureKind,
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
