using System.Diagnostics;

namespace CfoAgent.Api.Observability;

public sealed class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;
        var stopwatch = Stopwatch.StartNew();

        using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            logger.LogInformation("HTTP request started. Method: {Method}; Path: {Path}", context.Request.Method, context.Request.Path);
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation(
                    "HTTP request completed. Method: {Method}; Path: {Path}; StatusCode: {StatusCode}; DurationMilliseconds: {DurationMilliseconds}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var requestedId = context.Request.Headers[CorrelationHeader].ToString();
        return requestedId.Length is > 0 and <= 64 && requestedId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? requestedId
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
