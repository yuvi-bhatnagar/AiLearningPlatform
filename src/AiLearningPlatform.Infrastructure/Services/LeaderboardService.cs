using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AiLearningPlatform.Application.Features.Leaderboards;
using AiLearningPlatform.Application.Features.Leaderboards.DTOs;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private const string LeaderboardCacheKey = "LeaderboardData";

    public LeaderboardService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<LeaderboardRowDto>> GetLeaderboardAsync()
    {
        return await _cache.GetOrCreateAsync(LeaderboardCacheKey, async entry =>
        {
            // Cache absolute expiration set to 24 hours
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await FetchLeaderboardFromDbAsync();
        }) ?? new List<LeaderboardRowDto>();
    }

    public async Task<List<LeaderboardRowDto>> RecomputeLeaderboardCacheAsync()
    {
        var data = await FetchLeaderboardFromDbAsync();
        _cache.Set(LeaderboardCacheKey, data, TimeSpan.FromHours(24));
        return data;
    }

    private async Task<List<LeaderboardRowDto>> FetchLeaderboardFromDbAsync()
    {
        return await _context.Leaderboard
            .AsNoTracking()
            .OrderBy(l => l.Rank)
            .Select(l => new LeaderboardRowDto(l.UserId, l.Username, l.TotalScore, l.QuizzesAttempted, l.Rank))
            .ToListAsync();
    }

    public async Task<StudentPerformanceSummaryDto?> GetStudentPerformanceSummaryAsync(Guid userId)
    {
        // Execute the stored procedure and materialise as list first
        var results = await _context.StudentPerformanceSummaries
            .FromSqlInterpolated($"EXEC GetStudentPerformanceSummary {userId}")
            .AsNoTracking()
            .ToListAsync();

        var summary = results.FirstOrDefault();

        if (summary == null) return null;

        return new StudentPerformanceSummaryDto(
            summary.UserId,
            summary.Username,
            summary.TotalScore,
            summary.AverageScore,
            summary.TotalAttempts,
            summary.HighestScore,
            summary.LowestScore
        );
    }
}
