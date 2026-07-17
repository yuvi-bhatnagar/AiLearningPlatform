namespace AiLearningPlatform.Application.Features.Leaderboards.DTOs;

public record LeaderboardRowDto(
    Guid UserId,
    string Username,
    double TotalScore,
    int QuizzesAttempted,
    int Rank
);
