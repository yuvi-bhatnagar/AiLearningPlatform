namespace AiLearningPlatform.Application.Features.Leaderboards.DTOs;

public record StudentPerformanceSummaryDto(
    Guid UserId,
    string Username,
    double TotalScore,
    double AverageScore,
    int TotalAttempts,
    double HighestScore,
    double LowestScore
);
