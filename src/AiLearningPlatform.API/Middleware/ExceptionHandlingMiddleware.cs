using System.Net;
using System.Text.Json;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.API.Middleware;

// Why global exception middleware?
// In a clean architecture, we don't want try-catch blocks cluttering every controller action.
// The service layer throws structured domain exceptions when validation, logic, or authorization rules fail.
// This middleware runs at the root of the HTTP pipeline, catching any unhandled exceptions,
// logging them, and formatting a consistent, friendly JSON error shape back to the client.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during the request.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An internal server error occurred.";
        IDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                message = valEx.Message;
                errors = valEx.Errors;
                break;

            case NotFoundException nfEx:
                statusCode = HttpStatusCode.NotFound;
                message = nfEx.Message;
                break;

            case UnauthorizedAccessException uaEx:
                statusCode = HttpStatusCode.Forbidden;
                message = uaEx.Message;
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        // If in Development, we can include the stack trace for easier debugging
        var responsePayload = new
        {
            status = (int)statusCode,
            message,
            errors,
            detail = _env.IsDevelopment() ? exception.StackTrace : null
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var result = JsonSerializer.Serialize(responsePayload, jsonOptions);
        return context.Response.WriteAsync(result);
    }
}
