using AiLearningPlatform.Application.Features.Leaderboards.DTOs;

namespace AiLearningPlatform.Application.Features.Leaderboards;

public interface ILeaderboardService
{
    Task<List<LeaderboardRowDto>> GetLeaderboardAsync();
    Task<List<LeaderboardRowDto>> RecomputeLeaderboardCacheAsync();
    Task<StudentPerformanceSummaryDto?> GetStudentPerformanceSummaryAsync(Guid userId);
}
