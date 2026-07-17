using FluentAssertions;
using Moq;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Attempts.Jobs;
using AiLearningPlatform.Application.Features.AI.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Tests;

public class AiGradingJobTests
{
    private readonly Mock<IAttemptRepository> _attemptRepoMock = new();
    private readonly Mock<IAiService> _aiServiceMock = new();
    private readonly AiGradingJob _job;

    public AiGradingJobTests()
    {
        _job = new AiGradingJob(_attemptRepoMock.Object, _aiServiceMock.Object);
    }

    [Fact]
    public async Task GradeSubjectiveAnswersAsync_WithValidSubjectiveAnswer_ShouldGradeAndScaleScore()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        var qId = Guid.NewGuid();

        var quiz = new Quiz
        {
            Id = quizId,
            Questions = new List<Question>
            {
                new Question { Id = qId, QuizId = quizId, Type = QuestionType.Subjective, CorrectAnswer = "Key concepts", Points = 20 }
            }
        };

        var attempt = new Attempt
        {
            Id = attemptId,
            QuizId = quizId,
            Status = AttemptStatus.PendingGrading,
            Quiz = quiz,
            AnswerSubmissions = new List<AnswerSubmission>
            {
                new AnswerSubmission { AttemptId = attemptId, QuestionId = qId, StudentAnswer = "Student essay answer" }
            }
        };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _attemptRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var eval = new AiEvaluationResultDto(7.5, true, "Good explanation.", "High");
        _aiServiceMock.Setup(ai => ai.EvaluateAnswerAsync("Key concepts", "Key concepts", "Student essay answer")) // wait, parameters: questionText, correctAnswerKeywords, studentAnswer
            .ReturnsAsync(eval);
        // Wait, the parameters in EvaluateAnswerAsync:
        // Task<AiEvaluationResultDto> EvaluateAnswerAsync(string questionText, string correctAnswerKeywords, string studentAnswer);
        // In quiz question: Text is "Question text", CorrectAnswer is "Key concepts".
        // Let's setup quiz question properly: Text = "Explain database indexing"
        quiz.Questions.First().Text = "Explain database indexing";

        _aiServiceMock.Setup(ai => ai.EvaluateAnswerAsync("Explain database indexing", "Key concepts", "Student essay answer"))
            .ReturnsAsync(eval);

        // Act
        await _job.GradeSubjectiveAnswersAsync(attemptId);

        // Assert
        attempt.Status.Should().Be(AttemptStatus.Graded);
        attempt.Score.Should().Be(15.0); // 7.5/10 * 20 = 15.0 points

        var sub = attempt.AnswerSubmissions.First();
        sub.IsCorrect.Should().BeTrue();
        sub.Score.Should().Be(15.0);
        sub.Feedback.Should().Be("Good explanation.");

        _attemptRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GradeSubjectiveAnswersAsync_WhenAiThrows_ShouldFallbackGracefullyAndNotCrash()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        var qId = Guid.NewGuid();

        var quiz = new Quiz
        {
            Id = quizId,
            Questions = new List<Question>
            {
                new Question { Id = qId, QuizId = quizId, Type = QuestionType.Subjective, Text = "Explain database indexing", CorrectAnswer = "Key concepts", Points = 10 }
            }
        };

        var attempt = new Attempt
        {
            Id = attemptId,
            QuizId = quizId,
            Status = AttemptStatus.PendingGrading,
            Quiz = quiz,
            AnswerSubmissions = new List<AnswerSubmission>
            {
                new AnswerSubmission { AttemptId = attemptId, QuestionId = qId, StudentAnswer = "Student essay answer" }
            }
        };

        _attemptRepoMock.Setup(repo => repo.GetByIdAsync(attemptId))
            .ReturnsAsync(attempt);
        _attemptRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        _aiServiceMock.Setup(ai => ai.EvaluateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Gemini service unavailable"));

        // Act
        var act = () => _job.GradeSubjectiveAnswersAsync(attemptId);

        // Assert
        await act.Should().NotThrowAsync();

        attempt.Status.Should().Be(AttemptStatus.Graded);
        attempt.Score.Should().Be(0);

        var sub = attempt.AnswerSubmissions.First();
        sub.IsCorrect.Should().BeFalse();
        sub.Score.Should().Be(0);
        sub.Feedback.Should().Contain("AI Evaluation failed");

        _attemptRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }
}
