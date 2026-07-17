namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record AnswerSubmissionDto(
    Guid QuestionId,
    string StudentAnswer,
    bool? IsCorrect,
    double? Score,
    string? Feedback,
    string? Confidence
);
