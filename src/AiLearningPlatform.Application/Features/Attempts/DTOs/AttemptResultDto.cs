namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record AttemptResultDto(
    Guid Id,
    Guid QuizId,
    Guid UserId,
    DateTime StartedAtUtc,
    DateTime? SubmittedAtUtc,
    double? Score,
    string Status,
    List<AnswerSubmissionDto> Submissions
);
