using System.Net;
using CfoAgent.Api.Rag.Retrieval;

namespace CfoAgent.Api.Rag.Chroma;

public sealed class ChromaDependencyException : VectorSearchDependencyException
{
    public ChromaDependencyException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
