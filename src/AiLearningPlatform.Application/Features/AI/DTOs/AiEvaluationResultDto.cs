namespace AiLearningPlatform.Application.Features.AI.DTOs;

public record AiEvaluationResultDto(
    double Score,
    bool IsCorrect,
    string Feedback,
    string Confidence
);
