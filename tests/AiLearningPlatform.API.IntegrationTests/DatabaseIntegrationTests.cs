using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.IntegrationTests;

public class DatabaseIntegrationTests
{
    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Can_Save_And_Retrieve_User_With_AuthoredCourses()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var instructor = new User
        {
            Id = Guid.NewGuid(),
            Username = "teacher1",
            Email = "teacher1@example.com",
            PasswordHash = "hashedpassword",
            Role = UserRole.Teacher,
            CreatedAtUtc = DateTime.UtcNow
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = "C# Fundamentals",
            Description = "Learn C# programming",
            InstructorId = instructor.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        context.Users.Add(instructor);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        // Assert
        using var assertContext = CreateDbContext(dbName);
        var dbUser = await assertContext.Users
            .Include(u => u.AuthoredCourses)
            .FirstOrDefaultAsync(u => u.Id == instructor.Id);

        dbUser.Should().NotBeNull();
        dbUser!.Username.Should().Be("teacher1");
        dbUser.Role.Should().Be(UserRole.Teacher);
        dbUser.AuthoredCourses.Should().ContainSingle();
        dbUser.AuthoredCourses.First().Title.Should().Be("C# Fundamentals");
    }

    [Fact]
    public async Task Can_Save_And_Retrieve_Quiz_With_Questions()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var instructor = new User
        {
            Id = Guid.NewGuid(),
            Username = "teacher_quiz",
            Email = "teacher_quiz@example.com",
            PasswordHash = "hashedpassword",
            Role = UserRole.Teacher
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = "Introduction to EF Core",
            InstructorId = instructor.Id
        };

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "EF Core Basics Quiz",
            CourseId = course.Id
        };

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What does EF stand for?",
            Type = QuestionType.MultipleChoice,
            OptionsJson = "[\"Entity Framework\",\"Entity Fast\",\"Entry Framework\"]",
            CorrectAnswer = "Entity Framework",
            Points = 10
        };

        // Act
        context.Users.Add(instructor);
        context.Courses.Add(course);
        context.Quizzes.Add(quiz);
        context.Questions.Add(question);
        await context.SaveChangesAsync();

        // Assert
        using var assertContext = CreateDbContext(dbName);
        var dbQuiz = await assertContext.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quiz.Id);

        dbQuiz.Should().NotBeNull();
        dbQuiz!.Title.Should().Be("EF Core Basics Quiz");
        dbQuiz.Questions.Should().ContainSingle();
        dbQuiz.Questions.First().Text.Should().Be("What does EF stand for?");
        dbQuiz.Questions.First().Type.Should().Be(QuestionType.MultipleChoice);
    }
}
