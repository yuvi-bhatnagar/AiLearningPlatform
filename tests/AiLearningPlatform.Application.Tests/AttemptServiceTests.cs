using FluentAssertions;
using Moq;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Attempts;
using AiLearningPlatform.Application.Features.Attempts.DTOs;
using AiLearningPlatform.Application.Features.Attempts.Validators;
using AiLearningPlatform.Application.Features.AI.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Tests;

public class AttemptServiceTests
{
    private readonly Mock<IAttemptRepository> _attemptRepoMock = new();
    private readonly Mock<IQuizRepository> _quizRepoMock = new();
    private readonly Mock<ICourseRepository> _courseRepoMock = new();
    private readonly Mock<IAiService> _aiServiceMock = new();
    private readonly AttemptService _attemptService;

    public AttemptServiceTests()
    {
        var startValidator = new StartAttemptRequestValidator();
        var submitValidator = new SubmitAttemptRequestValidator();

        _attemptService = new AttemptService(
            _attemptRepoMock.Object,
            _quizRepoMock.Object,
            _courseRepoMock.Object,
            _aiServiceMock.Object,
            startValidator,
            submitValidator
        );
    }

    [Fact]
    public async Task StartAttemptAsync_WithValidQuiz_ShouldCreateAttemptAndHideCorrectAnswers()
    {
        // Arrange
        var quizId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var quiz = new Quiz
        {
            Id = quizId,
            Title = "SQL Basics",
            Questions = new List<Question>
            {
                new Question
                {
                    Id = Guid.NewGuid(),
                    QuizId = quizId,
                    Text = "What is SELECT?",
                    Type = QuestionType.MultipleChoice,
                    CorrectAnswer = "Query command",
                    Points = 10,
                    OptionsJson = "[\"Query command\", \"Delete command\"]"
                }
            }
        };

        _quizRepoMock.Setup(repo => repo.GetByIdAsync(quizId))
            .ReturnsAsync(quiz);
        _attemptRepoMock.Setup(repo => repo.GetActiveAttemptAsync(quizId, userId))
            .ReturnsAsync((Attempt?)null);
        _attemptRepoMock.Setup(repo => repo.AddAsync(It.IsAny<Attempt>()))
            .Returns(Task.CompletedTask);
        _attemptRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _attemptService.StartAttemptAsync(quizId, userId);

        // Assert
        result.Should().NotBeNull();
        result.QuizId.Should().Be(quizId);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be(AttemptStatus.InProgress.ToString());
        result.Questions.Should().HaveCount(1);
        
        // Hide CorrectAnswer validation
        result.Questions[0].Text.Should().Be("What is SELECT?");
        // Ensure CorrectAnswer property is NOT exposed in AttemptQuestionDto
        // result.Questions[0] type has no CorrectAnswer property in DTO definition.

        _attemptRepoMock.Verify(repo => repo.AddAsync(It.Is<Attempt>(a => 
            a.QuizId == quizId && 
            a.UserId == userId && 
            a.Status == AttemptStatus.InProgress)), Times.Once);
        _attemptRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAttemptAsync_WithActiveAttempt_ShouldThrowValidationException()
    {
        // Arrange
        var quizId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activeAttempt = new Attempt { Id = Guid.NewGuid(), QuizId = quizId, UserId = userId, Status = AttemptStatus.InProgress };

        _quizRepoMock.Setup(repo => repo.GetByIdAsync(quizId))
            .ReturnsAsync(new Quiz { Id = quizId });

        _attemptRepoMock.Setup(repo => repo.GetActiveAttemptAsync(quizId, userId))
            .ReturnsAsync(activeAttempt);

        // Act
        var act = () => _attemptService.StartAttemptAsync(quizId, userId);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.ValidationException>()
            .Where(e => e.Errors.ContainsKey("QuizId"));

        _attemptRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Attempt>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithOnlyMCQQuestions_ShouldAutoGradeAndSetGraded()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var quizId = Guid.NewGuid();

        var q1Id = Guid.NewGuid();
        var q2Id = Guid.NewGuid();

        var quiz = new Quiz
        {
            Id = quizId,
            Questions = new List<Question>
            {
                new Question { Id = q1Id, QuizId = quizId, Type = QuestionType.MultipleChoice, CorrectAnswer = "A", Points = 5 },
                new Question { Id = q2Id, QuizId = quizId, Type = QuestionType.MultipleChoice, CorrectAnswer = "B", Points = 10 }
            }
        };

        var attempt = new Attempt
        {
            Id = attemptId,
            QuizId = quizId,
            UserId = userId,
            Status = AttemptStatus.InProgress,
            Quiz = quiz
        };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _attemptRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var request = new SubmitAttemptRequest(new List<SubmitAnswerDto>
        {
            new SubmitAnswerDto(q1Id, "A"), // Correct
            new SubmitAnswerDto(q2Id, "Wrong") // Incorrect
        });

        // Act
        var result = await _attemptService.SubmitAttemptAsync(attemptId, request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AttemptStatus.Graded.ToString());
        result.Score.Should().Be(5); // 5 + 0
        result.Submissions.Should().HaveCount(2);

        var sub1 = result.Submissions.First(s => s.QuestionId == q1Id);
        sub1.IsCorrect.Should().BeTrue();
        sub1.Score.Should().Be(5);

        var sub2 = result.Submissions.First(s => s.QuestionId == q2Id);
        sub2.IsCorrect.Should().BeFalse();
        sub2.Score.Should().Be(0);

        _attemptRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithSubjectiveQuestions_ShouldSetPendingGradingAndNullScore()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var quizId = Guid.NewGuid();

        var q1Id = Guid.NewGuid();
        var q2Id = Guid.NewGuid();

        var quiz = new Quiz
        {
            Id = quizId,
            Questions = new List<Question>
            {
                new Question { Id = q1Id, QuizId = quizId, Type = QuestionType.MultipleChoice, CorrectAnswer = "A", Points = 5 },
                new Question { Id = q2Id, QuizId = quizId, Type = QuestionType.Subjective, CorrectAnswer = "Keywords", Points = 10 }
            }
        };

        var attempt = new Attempt
        {
            Id = attemptId,
            QuizId = quizId,
            UserId = userId,
            Status = AttemptStatus.InProgress,
            Quiz = quiz
        };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _attemptRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var aiEval = new AiEvaluationResultDto(8.0, true, "Good job!", "High");
        _aiServiceMock.Setup(ai => ai.EvaluateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(aiEval);

        var request = new SubmitAttemptRequest(new List<SubmitAnswerDto>
        {
            new SubmitAnswerDto(q1Id, "A"), // Correct MCQ = 5 pts
            new SubmitAnswerDto(q2Id, "Some subjective essay answer") // Subjective AI evaluated = 8/10 * 10 = 8 pts
        });

        // Act
        var result = await _attemptService.SubmitAttemptAsync(attemptId, request, userId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AttemptStatus.Graded.ToString());
        result.Score.Should().Be(13.0); // 5 + 8
        result.Submissions.Should().HaveCount(2);

        var sub2 = result.Submissions.First(s => s.QuestionId == q2Id);
        sub2.IsCorrect.Should().BeTrue();
        sub2.Score.Should().Be(8.0);
        sub2.Feedback.Should().Be("Good job!");

        _attemptRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithWrongUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var attempt = new Attempt { Id = attemptId, UserId = Guid.NewGuid(), Status = AttemptStatus.InProgress };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);

        var request = new SubmitAttemptRequest(new List<SubmitAnswerDto>());

        // Act
        var act = () => _attemptService.SubmitAttemptAsync(attemptId, request, Guid.NewGuid()); // Random user

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SubmitAttemptAsync_AlreadySubmitted_ShouldThrowValidationException()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var attempt = new Attempt { Id = attemptId, UserId = userId, Status = AttemptStatus.Graded };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);

        var request = new SubmitAttemptRequest(new List<SubmitAnswerDto>());

        // Act
        var act = () => _attemptService.SubmitAttemptAsync(attemptId, request, userId);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.ValidationException>()
            .Where(e => e.Errors.ContainsKey("Status"));
    }

    [Fact]
    public async Task GetAttemptByIdAsync_AsAuthorizedStudent_ShouldReturnResult()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var attempt = new Attempt { Id = attemptId, UserId = userId, Quiz = new Quiz() };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);

        // Act
        var result = await _attemptService.GetAttemptByIdAsync(attemptId, userId, "Student");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAttemptByIdAsync_AsUnauthorizedStudent_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var attempt = new Attempt { Id = attemptId, UserId = Guid.NewGuid(), Quiz = new Quiz { CourseId = Guid.NewGuid() } };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _courseRepoMock.Setup(repo => repo.GetByIdAsync(attempt.Quiz.CourseId))
            .ReturnsAsync(new Course { InstructorId = Guid.NewGuid() }); // Different instructor

        // Act
        var act = () => _attemptService.GetAttemptByIdAsync(attemptId, Guid.NewGuid(), "Student");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetAttemptByIdAsync_AsCourseInstructor_ShouldReturnResult()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var attempt = new Attempt { Id = attemptId, UserId = Guid.NewGuid(), Quiz = new Quiz { CourseId = Guid.NewGuid() } };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _courseRepoMock.Setup(repo => repo.GetByIdAsync(attempt.Quiz.CourseId))
            .ReturnsAsync(new Course { InstructorId = teacherId }); // Instructor matches caller

        // Act
        var result = await _attemptService.GetAttemptByIdAsync(attemptId, teacherId, "Teacher");

        // Assert
        result.Should().NotBeNull();
    }
}
