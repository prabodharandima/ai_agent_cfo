using System.Net;

namespace CfoAgent.Api.Rag.Chroma;

public sealed class ChromaDependencyException : Exception
{
    public ChromaDependencyException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
