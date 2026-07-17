using FluentAssertions;
using Moq;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Quizzes;
using AiLearningPlatform.Application.Features.Quizzes.DTOs;
using AiLearningPlatform.Application.Features.Quizzes.Validators;
using AiLearningPlatform.Application.Features.AI.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Tests;

public class QuizServiceTests
{
    private readonly Mock<IQuizRepository> _quizRepoMock = new();
    private readonly Mock<ICourseRepository> _courseRepoMock = new();
    private readonly Mock<IQuestionRepository> _questionRepoMock = new();
    private readonly Mock<IAiService> _aiServiceMock = new();
    private readonly QuizService _quizService;

    public QuizServiceTests()
    {
        var createValidator = new CreateQuizRequestValidator();
        var updateValidator = new UpdateQuizRequestValidator();

        _quizService = new QuizService(
            _quizRepoMock.Object,
            _courseRepoMock.Object,
            _questionRepoMock.Object,
            _aiServiceMock.Object,
            createValidator,
            updateValidator
        );
    }

    [Fact]
    public async Task GenerateQuestionsAsync_AsAuthorizedInstructor_ShouldCallAiServiceAndSaveQuestions()
    {
        // Arrange
        var quizId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        var quiz = new Quiz { Id = quizId, CourseId = courseId };
        var course = new Course { Id = courseId, InstructorId = instructorId };

        _quizRepoMock.Setup(repo => repo.GetByIdAsync(quizId))
            .ReturnsAsync(quiz);
        _courseRepoMock.Setup(repo => repo.GetByIdAsync(courseId))
            .ReturnsAsync(course);

        var generated = new List<GeneratedQuestionDto>
        {
            new GeneratedQuestionDto("Question 1", new List<string>{"A", "B"}, "A", 10)
        };

        _aiServiceMock.Setup(ai => ai.GenerateQuizAsync("AI topic", 1))
            .ReturnsAsync(generated);

        _questionRepoMock.Setup(repo => repo.AddAsync(It.IsAny<Question>()))
            .Returns(Task.CompletedTask);
        _questionRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _quizService.GenerateQuestionsAsync(quizId, "AI topic", 1, instructorId, "Teacher");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Text.Should().Be("Question 1");
        result.First().Points.Should().Be(10);

        _aiServiceMock.Verify(ai => ai.GenerateQuizAsync("AI topic", 1), Times.Once);
        _questionRepoMock.Verify(repo => repo.AddAsync(It.Is<Question>(q => 
            q.QuizId == quizId && 
            q.Text == "Question 1" && 
            q.CorrectAnswer == "A")), Times.Once);
        _questionRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateQuestionsAsync_AsUnauthorizedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var quizId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();

        var quiz = new Quiz { Id = quizId, CourseId = courseId };
        var course = new Course { Id = courseId, InstructorId = instructorId };

        _quizRepoMock.Setup(repo => repo.GetByIdAsync(quizId))
            .ReturnsAsync(quiz);
        _courseRepoMock.Setup(repo => repo.GetByIdAsync(courseId))
            .ReturnsAsync(course);

        // Act
        var act = () => _quizService.GenerateQuestionsAsync(quizId, "Topic", 1, Guid.NewGuid(), "Teacher"); // Wrong user

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _aiServiceMock.Verify(ai => ai.GenerateQuizAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
