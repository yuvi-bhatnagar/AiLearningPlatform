namespace AiLearningPlatform.Domain.Exceptions;

// Throw this when requests fail FluentValidation checks or custom validation rules.
// The global exception middleware will catch this and map it to HTTP 400 Bad Request,
// formatting the errors list cleanly.
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("One or more inputs are invalid.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more inputs are invalid.")
    {
        Errors = errors;
    }
}
