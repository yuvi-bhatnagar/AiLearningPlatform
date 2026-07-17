namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record AttemptDto(
    Guid Id,
    Guid QuizId,
    Guid UserId,
    DateTime StartedAtUtc,
    string Status,
    List<AttemptQuestionDto> Questions
);
