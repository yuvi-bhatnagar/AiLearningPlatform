using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Features.Leaderboards;
using AiLearningPlatform.Application.Features.Leaderboards.Jobs;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.BackgroundJobs;

public class NightlyMaintenanceJob : INightlyMaintenanceJob
{
    private readonly AppDbContext _context;
    private readonly ILeaderboardService _leaderboardService;

    public NightlyMaintenanceJob(AppDbContext context, ILeaderboardService leaderboardService)
    {
        _context = context;
        _leaderboardService = leaderboardService;
    }

    public async Task RunNightlyMaintenanceAsync()
    {
        // 1. Recompute leaderboard cache
        await _leaderboardService.RecomputeLeaderboardCacheAsync();

        // 2. Reset streaks
        var cutoff = DateTime.UtcNow.Date.AddDays(-1);
        var usersToReset = await _context.Users
            .Where(u => u.Streak > 0 && (u.LastAttemptDateUtc == null || u.LastAttemptDateUtc < cutoff))
            .ToListAsync();

        foreach (var user in usersToReset)
        {
            user.Streak = 0;
        }

        await _context.SaveChangesAsync();
    }
}
