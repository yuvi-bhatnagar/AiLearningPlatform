using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.API.IntegrationTests;

public class CourseCrudTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestWebAppFactory _factory;

    public CourseCrudTests(AuthTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Helper: Register/Login a user and configure the authorization header on the client
    private async Task<AuthResponse> AuthenticateClientAsync(string username, UserRole role)
    {
        var email = $"{username}_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterRequest(username + "_" + Guid.NewGuid().ToString("N")[..6], email, "Password123!", role);
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var authBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authBody!.AccessToken);

        return authBody;
    }

    [Fact]
    public async Task Course_FullCrudFlow_AsTeacher_ShouldSucceed()
    {
        // 1. Authenticate as Teacher
        var auth = await AuthenticateClientAsync("teacher_crud", UserRole.Teacher);

        // 2. Create Course (POST)
        var createRequest = new CreateCourseRequest("Advanced EF Core 2026", "Learn performance optimization in EF Core");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/courses", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        createdCourse.Should().NotBeNull();
        createdCourse!.Id.Should().NotBeEmpty();
        createdCourse.Title.Should().Be(createRequest.Title);
        createdCourse.InstructorId.Should().Be(auth.UserId);

        // 3. Retrieve Course by ID (GET)
        // Reset authorization to Anonymous to test public read access
        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/api/v1/courses/{createdCourse.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var fetchedCourse = await getResponse.Content.ReadFromJsonAsync<CourseDto>();
        fetchedCourse.Should().NotBeNull();
        fetchedCourse!.Title.Should().Be(createRequest.Title);

        // 4. Update Course (PUT)
        // Authenticate back as the teacher
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var updateRequest = new UpdateCourseRequest("Advanced EF Core 2026 (Updated)", "Updated description");
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/courses/{createdCourse.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedCourse = await updateResponse.Content.ReadFromJsonAsync<CourseDto>();
        updatedCourse!.Title.Should().Be(updateRequest.Title);
        updatedCourse.Description.Should().Be(updateRequest.Description);

        // 5. Delete Course (DELETE)
        var deleteResponse = await _client.DeleteAsync($"/api/v1/courses/{createdCourse.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it was deleted (should return 404)
        var getAfterDeleteResponse = await _client.GetAsync($"/api/v1/courses/{createdCourse.Id}");
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCourse_AsStudent_ShouldReturn403Forbidden()
    {
        // 1. Authenticate as Student
        await AuthenticateClientAsync("student_crud", UserRole.Student);

        // 2. Try to Create Course (POST)
        var createRequest = new CreateCourseRequest("Student Attempted Course", "Should fail");
        var response = await _client.PostAsJsonAsync("/api/v1/courses", createRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCourse_AsAnonymous_ShouldReturn401Unauthorized()
    {
        // No authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        var createRequest = new CreateCourseRequest("Anon Attempted Course", "Should fail");
        var response = await _client.PostAsJsonAsync("/api/v1/courses", createRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCourse_WithInvalidData_ShouldReturn400BadRequest()
    {
        // 1. Authenticate as Teacher
        await AuthenticateClientAsync("teacher_invalid", UserRole.Teacher);

        // 2. Try to Create Course with empty Title (POST)
        var createRequest = new CreateCourseRequest("", "Valid description");
        var response = await _client.PostAsJsonAsync("/api/v1/courses", createRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Check structured error message shape returned by ExceptionHandlingMiddleware
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("One or more inputs are invalid.");
        body.Should().Contain("Title is required.");
    }
}
