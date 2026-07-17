using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using AiLearningPlatform.Application.Features.Leaderboards.DTOs;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.IntegrationTests;

public class LeaderboardIntegrationTests : IClassFixture<AuthTestWebAppFactory>
{
    private readonly AuthTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public LeaderboardIntegrationTests(AuthTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string GenerateToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerateAccessToken(user);
    }

    [Fact]
    public async Task GetLeaderboard_ShouldReturnCorrectRankingAndScores()
    {
        // 1. Arrange: Create students and teacher manually
        var student1 = new User { Id = Guid.NewGuid(), Username = $"s1_{Guid.NewGuid():N}"[..15], Email = $"s1_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };
        var student2 = new User { Id = Guid.NewGuid(), Username = $"s2_{Guid.NewGuid():N}"[..15], Email = $"s2_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };
        var teacher = new User { Id = Guid.NewGuid(), Username = $"t_{Guid.NewGuid():N}"[..15], Email = $"t_{Guid.NewGuid():N}@edu.com", Role = UserRole.Teacher, PasswordHash = "hash" };

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Users.AddRange(student1, student2, teacher);
        await context.SaveChangesAsync();

        // 2. Create course and quiz
        var course = new Course { Id = Guid.NewGuid(), Title = "SQL Course", InstructorId = teacher.Id };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "SQL Quiz", CourseId = course.Id };
        
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        // 3. Create attempts
        var attempt1 = new Attempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = student1.Id,
            Score = 45.0,
            Status = AttemptStatus.Graded,
            StartedAtUtc = DateTime.UtcNow
        };

        var attempt2 = new Attempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = student2.Id,
            Score = 90.0,
            Status = AttemptStatus.Graded,
            StartedAtUtc = DateTime.UtcNow
        };

        context.Attempts.AddRange(attempt1, attempt2);
        await context.SaveChangesAsync();

        // 4. Force cache refresh by running the nightly job
        var maintenanceJob = scope.ServiceProvider.GetRequiredService<AiLearningPlatform.Application.Features.Leaderboards.Jobs.INightlyMaintenanceJob>();
        await maintenanceJob.RunNightlyMaintenanceAsync();

        // 5. Get leaderboard
        var s1Token = GenerateToken(student1);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s1Token);
        var response = await _client.GetAsync("/api/v1/leaderboards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var leaderboard = await response.Content.ReadFromJsonAsync<List<LeaderboardRowDto>>();

        leaderboard.Should().NotBeNull();
        leaderboard!.Should().HaveCountGreaterThanOrEqualTo(2);

        // Student 2 should be ranked 1st with score 90
        var s2Row = leaderboard!.First(x => x.UserId == student2.Id);
        s2Row.TotalScore.Should().Be(90.0);
        s2Row.Rank.Should().Be(1);

        // Student 1 should be ranked behind Student 2
        var s1Row = leaderboard!.First(x => x.UserId == student1.Id);
        s1Row.TotalScore.Should().Be(45.0);
        s1Row.Rank.Should().BeGreaterThan(s2Row.Rank);
    }

    [Fact]
    public async Task GetStudentPerformanceSummary_ShouldReturnStoredProcedureStats()
    {
        // Arrange: Create student and teacher
        var student = new User { Id = Guid.NewGuid(), Username = $"s_summary_{Guid.NewGuid():N}"[..15], Email = $"s_summary_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };
        var teacher = new User { Id = Guid.NewGuid(), Username = $"t_summary_{Guid.NewGuid():N}"[..15], Email = $"t_summary_{Guid.NewGuid():N}@edu.com", Role = UserRole.Teacher, PasswordHash = "hash" };

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Users.AddRange(student, teacher);
        await context.SaveChangesAsync();

        // Create course and quiz
        var course = new Course { Id = Guid.NewGuid(), Title = "API Course", InstructorId = teacher.Id };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "API Quiz", CourseId = course.Id };
        
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        // Seed 3 attempts for this student: scores 10, 20, 30
        var attempt1 = new Attempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = student.Id, Score = 10.0, Status = AttemptStatus.Graded, StartedAtUtc = DateTime.UtcNow };
        var attempt2 = new Attempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = student.Id, Score = 20.0, Status = AttemptStatus.Graded, StartedAtUtc = DateTime.UtcNow };
        var attempt3 = new Attempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = student.Id, Score = 30.0, Status = AttemptStatus.Graded, StartedAtUtc = DateTime.UtcNow };

        context.Attempts.AddRange(attempt1, attempt2, attempt3);
        await context.SaveChangesAsync();

        var token = GenerateToken(student);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/leaderboards/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<StudentPerformanceSummaryDto>();

        summary.Should().NotBeNull();
        summary!.UserId.Should().Be(student.Id);
        summary.TotalAttempts.Should().Be(3);
        summary.TotalScore.Should().Be(60.0); // 10 + 20 + 30
        summary.AverageScore.Should().Be(20.0); // 60 / 3
        summary.HighestScore.Should().Be(30.0);
        summary.LowestScore.Should().Be(10.0);
    }

    [Fact]
    public async Task GetUserSummary_ForOtherStudent_ByStudent_ShouldReturnForbidden()
    {
        // Arrange: Create student 1 and student 2
        var student1 = new User { Id = Guid.NewGuid(), Username = $"s1_auth_{Guid.NewGuid():N}"[..15], Email = $"s1_auth_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };
        var student2 = new User { Id = Guid.NewGuid(), Username = $"s2_auth_{Guid.NewGuid():N}"[..15], Email = $"s2_auth_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Users.AddRange(student1, student2);
        await context.SaveChangesAsync();

        var s1Token = GenerateToken(student1);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s1Token);

        // Act
        var response = await _client.GetAsync($"/api/v1/leaderboards/summary/{student2.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserSummary_ForOtherStudent_ByTeacherOrAdmin_ShouldSucceed()
    {
        // Arrange: Create student and teacher
        var student = new User { Id = Guid.NewGuid(), Username = $"s_ta_{Guid.NewGuid():N}"[..15], Email = $"s_ta_{Guid.NewGuid():N}@edu.com", Role = UserRole.Student, PasswordHash = "hash" };
        var teacher = new User { Id = Guid.NewGuid(), Username = $"t_ta_{Guid.NewGuid():N}"[..15], Email = $"t_ta_{Guid.NewGuid():N}@edu.com", Role = UserRole.Teacher, PasswordHash = "hash" };

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Users.AddRange(student, teacher);
        await context.SaveChangesAsync();

        var course = new Course { Id = Guid.NewGuid(), Title = "Web Course", InstructorId = teacher.Id }; 
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Web Quiz", CourseId = course.Id };
        
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        var attempt = new Attempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = student.Id, Score = 50.0, Status = AttemptStatus.Graded, StartedAtUtc = DateTime.UtcNow };
        context.Attempts.Add(attempt);
        await context.SaveChangesAsync();

        var teacherToken = GenerateToken(teacher);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

        // Act
        var response = await _client.GetAsync($"/api/v1/leaderboards/summary/{student.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<StudentPerformanceSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalScore.Should().Be(50.0);
    }
}
