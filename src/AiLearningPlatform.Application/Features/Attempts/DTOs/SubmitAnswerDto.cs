namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record SubmitAnswerDto(
    Guid QuestionId,
    string StudentAnswer
);
