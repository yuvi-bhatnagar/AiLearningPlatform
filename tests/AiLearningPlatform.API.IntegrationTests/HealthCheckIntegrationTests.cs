using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiLearningPlatform.API.IntegrationTests;

public class HealthCheckIntegrationTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly AuthTestWebAppFactory _factory;

    public HealthCheckIntegrationTests(AuthTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_WhenAllDependenciesUp_ShouldReturn200AndHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var bodyText = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Health check response was: {bodyText}");
        
        var body = JsonSerializer.Deserialize<JsonElement>(bodyText);
        body.GetProperty("status").GetString().Should().Be("Healthy");

        var checks = body.GetProperty("checks");
        checks.ValueKind.Should().Be(JsonValueKind.Array);
        checks.GetArrayLength().Should().Be(3); // sqlserver, redis, hangfire
    }

    [Fact]
    public async Task GetHealth_WhenDatabaseDown_ShouldReturn503AndUnhealthy()
    {
        // Arrange
        using var downFactory = new DatabaseDownTestFactory();
        await downFactory.InitializeAsync();
        var client = downFactory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable); // 503
        
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    private class DatabaseDownTestFactory : AuthTestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost,9999;Database=AiLearningDb;User Id=sa;Password=Wrong;Connect Timeout=2;");
        }
    }
}
