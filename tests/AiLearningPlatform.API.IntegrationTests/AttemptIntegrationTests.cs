using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using AiLearningPlatform.Application.Features.Quizzes.DTOs;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Application.Features.Attempts.DTOs;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.IntegrationTests;

public class AttemptIntegrationTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestWebAppFactory _factory;

    public AttemptIntegrationTests(AuthTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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
    public async Task Student_QuizAttemptFlow_MCQOnly_ShouldSucceedAndAutoGrade()
    {
        // 1. Setup Quiz by Teacher
        var teacherAuth = await AuthenticateClientAsync("teacher_attempt_test", UserRole.Teacher);

        // Create Course
        var createCourseResponse = await _client.PostAsJsonAsync("/api/v1/courses", new CreateCourseRequest("EF Core Course", "Desc"));
        var course = await createCourseResponse.Content.ReadFromJsonAsync<CourseDto>();

        // Create Quiz
        var createQuizResponse = await _client.PostAsJsonAsync("/api/v1/quizzes", new CreateQuizRequest("EF Core MCQ Quiz", "Desc", course!.Id));
        var quiz = await createQuizResponse.Content.ReadFromJsonAsync<QuizDto>();

        // Create MCQ Question
        var createQResponse = await _client.PostAsJsonAsync("/api/v1/questions", new CreateQuestionRequest(
            quiz!.Id,
            "What does EF stand for?",
            QuestionType.MultipleChoice,
            new List<string> { "Entity Framework", "Error Free" },
            "Entity Framework",
            10
        ));
        var question = await createQResponse.Content.ReadFromJsonAsync<QuestionDto>();

        // 2. Authenticate as Student
        var studentAuth = await AuthenticateClientAsync("student_attempt_test", UserRole.Student);

        // Start Attempt (POST /api/v1/attempts/start)
        var startRequest = new StartAttemptRequest(quiz.Id);
        var startResponse = await _client.PostAsJsonAsync("/api/v1/attempts/start", startRequest);
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var attempt = await startResponse.Content.ReadFromJsonAsync<AttemptDto>();
        attempt.Should().NotBeNull();
        attempt!.QuizId.Should().Be(quiz.Id);
        attempt.Status.Should().Be(AttemptStatus.InProgress.ToString());
        attempt.Questions.Should().HaveCount(1);
        attempt.Questions[0].Id.Should().Be(question!.Id);

        // Submit Attempt (POST /api/v1/attempts/{id}/submit)
        var submitRequest = new SubmitAttemptRequest(new List<SubmitAnswerDto>
        {
            new SubmitAnswerDto(question.Id, "Entity Framework") // Correct answer
        });
        var submitResponse = await _client.PostAsJsonAsync($"/api/v1/attempts/{attempt.Id}/submit", submitRequest);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await submitResponse.Content.ReadFromJsonAsync<AttemptResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(AttemptStatus.Graded.ToString());
        result.Score.Should().Be(10);
        result.Submissions.Should().HaveCount(1);
        result.Submissions[0].IsCorrect.Should().BeTrue();
        result.Submissions[0].Score.Should().Be(10);

        // Retrieve Attempt (GET /api/v1/attempts/{id})
        var getResponse = await _client.GetAsync($"/api/v1/attempts/{attempt.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResult = await getResponse.Content.ReadFromJsonAsync<AttemptResultDto>();
        getResult.Should().NotBeNull();
        getResult!.Score.Should().Be(10);
        getResult.Status.Should().Be(AttemptStatus.Graded.ToString());
    }

    [Fact]
    public async Task StartAttempt_ForInvalidQuiz_ShouldReturn404()
    {
        // 1. Authenticate as Student
        await AuthenticateClientAsync("student_attempt_404", UserRole.Student);

        // 2. Try to Start Attempt for non-existing Quiz
        var startRequest = new StartAttemptRequest(Guid.NewGuid());
        var startResponse = await _client.PostAsJsonAsync("/api/v1/attempts/start", startRequest);
        startResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Student_QuizAttemptFlow_Subjective_ShouldEnqueueJobAndPendingGrading()
    {
        // Arrange
        var username = "std_subjective_" + Guid.NewGuid().ToString("N")[..6];
        var email = username + "@example.com";
        var studentToken = (await AuthenticateClientAsync(username, UserRole.Student)).AccessToken;

        // Seed subjective question
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var teacher = new User { Id = Guid.NewGuid(), Username = "TeacherSub_" + Guid.NewGuid().ToString("N")[..6], Email = "teachersub_" + Guid.NewGuid().ToString("N")[..6] + "@edu.com", PasswordHash = "hash", Role = UserRole.Teacher };
        var course = new Course { Id = Guid.NewGuid(), Title = "Course", Instructor = teacher };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Quiz", Course = course };
        var question = new Question { Id = Guid.NewGuid(), QuizId = quiz.Id, Type = QuestionType.Subjective, Text = "Explain selective queries", CorrectAnswer = "SELECT keyword", Points = 10 };

        context.Users.Add(teacher);
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        context.Questions.Add(question);
        await context.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // Start Attempt
        var startRequest = new StartAttemptRequest(quiz.Id);
        var startResponse = await _client.PostAsJsonAsync("/api/v1/attempts/start", startRequest);
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var attempt = await startResponse.Content.ReadFromJsonAsync<AttemptDto>();

        // Submit Attempt
        var submitRequest = new SubmitAttemptRequest(new List<SubmitAnswerDto>
        {
            new SubmitAnswerDto(question.Id, "Selective query uses SELECT statement.")
        });

        // Act
        var submitResponse = await _client.PostAsJsonAsync($"/api/v1/attempts/{attempt!.Id}/submit", submitRequest);

        // Assert
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await submitResponse.Content.ReadFromJsonAsync<AttemptResultDto>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(AttemptStatus.PendingGrading.ToString());
        result.Score.Should().BeNull();
        result.Submissions[0].IsCorrect.Should().BeNull();
    }
}
