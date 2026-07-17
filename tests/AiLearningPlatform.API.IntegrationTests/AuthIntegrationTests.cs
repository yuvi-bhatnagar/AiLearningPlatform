using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.AI.DTOs;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.IntegrationTests;

// Why a custom WebApplicationFactory?
// The default factory uses the real appsettings (real SQL Server) which means:
//   ❌ Tests require Docker to be running
//   ❌ Admin seed fails on second test run (duplicate key)
//   ❌ Tests leave dirty data in the real database
//   ❌ Tests are slow and non-deterministic
//
// By overriding the DbContext to use InMemory:
//   ✅ Tests run without Docker
//   ✅ Each factory gets a fresh database
//   ✅ Tests are fast (~milliseconds), isolated, and reproducible
//   ✅ CI/CD pipeline works without infrastructure dependencies
//
// Note: InMemory doesn't validate SQL constraints (unique keys, FKs) the same way.
// For constraint-level tests, use Testcontainers (Module 8) with a real SQL Server.
public class AuthTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync());

        // Apply migrations to setup schema in the container
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_dbContainer.GetConnectionString())
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await Task.WhenAll(_dbContainer.DisposeAsync().AsTask(), _redisContainer.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real SQL Server DbContextOptions registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Re-register AppDbContext using the Testcontainers connection string
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(_dbContainer.GetConnectionString());
            });

            // Remove the real Redis cache registration if it exists
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDistributedCache));
            if (redisDescriptor != null)
                services.Remove(redisDescriptor);

            // Re-register Redis cache using the Testcontainers connection string
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _redisContainer.GetConnectionString();
            });

            // Mock IAiService for all integration tests to prevent calling the actual Gemini API
            var aiServiceMock = new Mock<IAiService>();
            
            aiServiceMock.Setup(ai => ai.GenerateQuizAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((string topic, int count) =>
                {
                    var questions = new List<GeneratedQuestionDto>();
                    for (int i = 1; i <= count; i++)
                    {
                        questions.Add(new GeneratedQuestionDto(
                            $"AI Question {i} about {topic}",
                            new List<string> { "Option A", "Option B", "Option C", "Option D" },
                            "Option A",
                            10
                        ));
                    }
                    return questions;
                });
                
            aiServiceMock.Setup(ai => ai.EvaluateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new AiEvaluationResultDto(9.0, true, "AI feedback: Excellent!", "High"));

            services.AddSingleton<IAiService>(aiServiceMock.Object);

            var backgroundJobServiceMock = new Mock<IBackgroundJobService>();
            services.AddSingleton<IBackgroundJobService>(backgroundJobServiceMock.Object);
        });
    }
}

// Why IClassFixture<AuthTestWebAppFactory>?
// xUnit creates ONE factory instance shared across all tests in this class.
// This means all tests in this file share the SAME in-memory database.
// If test isolation within the class is needed, generate unique emails/usernames (we already do).
public class AuthIntegrationTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(AuthTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_Returns200WithTokens()
    {
        var request = new RegisterRequest(
            "testuser_" + Guid.NewGuid().ToString("N")[..8],
            $"test_{Guid.NewGuid():N}@example.com",
            "Password123!",
            UserRole.Student
        );

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty("a JWT access token should be returned");
        body.RefreshToken.Should().NotBeNullOrEmpty("a refresh token should be returned");
        body.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest("dupuser1_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", UserRole.Student);
        var request2 = new RegisterRequest("dupuser2_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", UserRole.Student);

        await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request2);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200WithTokens()
    {
        // First register a user
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest("loginuser_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", UserRole.Teacher);
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Then login
        var loginRequest = new LoginRequest(email, "Password123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.Role.Should().Be(UserRole.Teacher);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"wrongpass_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest("wrongpassuser_" + Guid.NewGuid().ToString("N")[..6], email, "CorrectPass123!", UserRole.Student);
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "WrongPassword!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // No Authorization header set — should get 401 Unauthorized
        var response = await _client.GetAsync("/api/v1/test-auth/authenticated");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        // Register and get a token
        var email = $"authtest_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest("authtestuser_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", UserRole.Student);
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var authBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Use the token in the Authorization header
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authBody!.AccessToken);

        var response = await _client.GetAsync("/api/v1/test-auth/authenticated");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Clean up for other tests
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task StudentEndpoint_WithTeacherToken_Returns403Forbidden()
    {
        // Register as Teacher
        var email = $"teacher_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest("teachertest_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", UserRole.Teacher);
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var authBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Use teacher's token to access a Student-only endpoint
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authBody!.AccessToken);

        // /student endpoint requires Role == "Student" — Teacher should get 403 Forbidden
        var response = await _client.GetAsync("/api/v1/test-auth/student");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
