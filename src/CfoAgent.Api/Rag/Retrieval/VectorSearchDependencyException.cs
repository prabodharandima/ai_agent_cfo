namespace CfoAgent.Api.Rag.Retrieval;

public abstract class VectorSearchDependencyException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
