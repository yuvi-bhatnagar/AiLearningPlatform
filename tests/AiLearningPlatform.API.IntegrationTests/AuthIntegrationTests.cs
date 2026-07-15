using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.API.IntegrationTests;

// Why WebApplicationFactory?
// WebApplicationFactory<Program> spins up a full in-memory instance of our ASP.NET Core app
// (including middleware, controllers, DI container) without needing a real HTTP server or port.
// This lets us test the FULL request pipeline — routing, auth middleware, model validation —
// all in a single test process.
public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Create test HTTP client with the in-memory test server
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
