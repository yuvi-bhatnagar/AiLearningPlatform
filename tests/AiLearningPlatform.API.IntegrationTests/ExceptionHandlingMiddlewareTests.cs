using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using AiLearningPlatform.API.Middleware;
using AiLearningPlatform.Domain.Exceptions;
using Xunit;

namespace AiLearningPlatform.API.IntegrationTests;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock = new();
    private readonly Mock<IHostEnvironment> _envMock = new();

    public ExceptionHandlingMiddlewareTests()
    {
        _envMock.Setup(e => e.EnvironmentName).Returns("Production");
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationExceptionThrown_ShouldReturnBadRequest()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Field", new[] { "Error message" } }
        };
        var exception = new ValidationException(errors);

        RequestDelegate next = (ctx) => throw exception;
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _envMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        
        var payload = JsonDocument.Parse(responseBody).RootElement;
        payload.GetProperty("status").GetInt32().Should().Be(400);
        payload.GetProperty("message").GetString().Should().Be("One or more inputs are invalid.");
        payload.GetProperty("errors").GetProperty("Field")[0].GetString().Should().Be("Error message");
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundExceptionThrown_ShouldReturnNotFound()
    {
        // Arrange
        var exception = new NotFoundException("Course", Guid.NewGuid());

        RequestDelegate next = (ctx) => throw exception;
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _envMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        var payload = JsonDocument.Parse(responseBody).RootElement;
        payload.GetProperty("status").GetInt32().Should().Be(404);
        payload.GetProperty("message").GetString().Should().Contain("could not be found");
    }
}
