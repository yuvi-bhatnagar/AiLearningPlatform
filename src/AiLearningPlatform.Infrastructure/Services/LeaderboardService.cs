using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using AiLearningPlatform.Application.Common.Extensions;
using AiLearningPlatform.Application.Features.Leaderboards;
using AiLearningPlatform.Application.Features.Leaderboards.DTOs;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _context;
    private readonly IDistributedCache _cache;
    private const string LeaderboardCacheKey = "LeaderboardData";

    public LeaderboardService(AppDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<LeaderboardRowDto>> GetLeaderboardAsync()
    {
        var cached = await _cache.GetRecordAsync<List<LeaderboardRowDto>>(LeaderboardCacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var data = await FetchLeaderboardFromDbAsync();
        await _cache.SetRecordAsync(LeaderboardCacheKey, data, TimeSpan.FromHours(24));
        return data;
    }

    public async Task<List<LeaderboardRowDto>> RecomputeLeaderboardCacheAsync()
    {
        var data = await FetchLeaderboardFromDbAsync();
        await _cache.SetRecordAsync(LeaderboardCacheKey, data, TimeSpan.FromHours(24));
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
