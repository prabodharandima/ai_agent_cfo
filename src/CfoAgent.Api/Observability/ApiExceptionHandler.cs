using CfoAgent.Api.Rag.Chroma;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CfoAgent.Api.Observability;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ChromaDependencyException => (StatusCodes.Status503ServiceUnavailable, "A required dependency is temporarily unavailable."),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            OperationCanceledException when !httpContext.RequestAborted.IsCancellationRequested => (StatusCodes.Status504GatewayTimeout, "The request timed out."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "The requested operation is temporarily unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
        };

        logger.LogError(
            "Unhandled request failure. CorrelationId: {CorrelationId}; FailureType: {FailureType}; StatusCode: {StatusCode}",
            httpContext.TraceIdentifier,
            exception.GetType().Name,
            statusCode);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
