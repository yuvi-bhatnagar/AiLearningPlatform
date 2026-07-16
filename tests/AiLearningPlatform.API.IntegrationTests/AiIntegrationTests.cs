using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using AiLearningPlatform.Application.Features.Quizzes.DTOs;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.IntegrationTests;

public class AiIntegrationTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly AuthTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public AiIntegrationTests(AuthTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid UserId)> RegisterAndLoginAsync(string email, UserRole role)
    {
        var username = email.Split('@')[0];
        var registerRequest = new RegisterRequest(username, email, "Pass123!", role);
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, "Pass123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var result = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return (result!.AccessToken, result.UserId);
    }

    [Fact]
    public async Task GenerateQuestions_AsTeacherOwner_ShouldSucceed()
    {
        // Arrange: Register teacher
        var teacherEmail = $"teacher_{Guid.NewGuid()}@edu.com";
        var (token, teacherId) = await RegisterAndLoginAsync(teacherEmail, UserRole.Teacher);

        // Seed Course and Quiz in DB
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = "Test Course",
            Description = "Desc",
            InstructorId = teacherId
        };
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Test Quiz",
            Description = "Desc",
            CourseId = course.Id
        };

        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new GenerateQuestionsRequest("Entity Framework", 3);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/quizzes/{quiz.Id}/generate-questions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<QuestionDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result![0].Text.Should().Contain("AI Question 1 about Entity Framework");
        result[0].Points.Should().Be(10);
    }

    [Fact]
    public async Task GenerateQuestions_AsStudent_ShouldReturnForbidden()
    {
        // Arrange: Register student and teacher
        var teacherEmail = $"teacher_{Guid.NewGuid()}@edu.com";
        var (_, teacherId) = await RegisterAndLoginAsync(teacherEmail, UserRole.Teacher);

        var studentEmail = $"student_{Guid.NewGuid()}@edu.com";
        var (studentToken, _) = await RegisterAndLoginAsync(studentEmail, UserRole.Student);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var course = new Course { Id = Guid.NewGuid(), Title = "Course", InstructorId = teacherId };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Quiz", CourseId = course.Id };
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var request = new GenerateQuestionsRequest("Topic", 2);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/quizzes/{quiz.Id}/generate-questions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AiEndpoints_RateLimiter_ShouldBlockRequestsBeyondThreshold()
    {
        // Arrange
        var teacherEmail = $"teacher_limiter_{Guid.NewGuid()}@edu.com";
        var (token, teacherId) = await RegisterAndLoginAsync(teacherEmail, UserRole.Teacher);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var course = new Course { Id = Guid.NewGuid(), Title = "Course", InstructorId = teacherId };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Quiz", CourseId = course.Id };
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new GenerateQuestionsRequest("Topic", 1);

        // Act & Assert: Send 5 successful requests, then the 6th should return 429 Too Many Requests
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsJsonAsync($"/api/v1/quizzes/{quiz.Id}/generate-questions", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var blockedResponse = await _client.PostAsJsonAsync($"/api/v1/quizzes/{quiz.Id}/generate-questions", request);
        blockedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
