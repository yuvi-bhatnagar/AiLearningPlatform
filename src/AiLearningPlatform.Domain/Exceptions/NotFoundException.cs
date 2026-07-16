namespace AiLearningPlatform.Domain.Exceptions;

// Throw this when a database lookup fails to find a record (e.g. Course/Quiz/Question).
// The global exception middleware will catch this and map it to HTTP 404 Not Found.
public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}
