using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using AiLearningPlatform.Application.Features.Leaderboards;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.BackgroundJobs;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Application.Tests;

public class NightlyMaintenanceJobTests
{
    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task RunNightlyMaintenance_ShouldResetStreaksOnlyIfInactiveSinceYesterday()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var now = DateTime.UtcNow;
        var today = now.Date;
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        // User 1: Active today - streak should NOT reset
        var userActiveToday = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_today",
            Email = "today@example.com",
            Streak = 3,
            LastAttemptDateUtc = now
        };

        // User 2: Active yesterday - streak should NOT reset
        var userActiveYesterday = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_yesterday",
            Email = "yesterday@example.com",
            Streak = 5,
            LastAttemptDateUtc = yesterday.AddHours(12) // yesterday afternoon
        };

        // User 3: Active two days ago - streak SHOULD reset
        var userActiveTwoDaysAgo = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_twodays",
            Email = "twodays@example.com",
            Streak = 4,
            LastAttemptDateUtc = twoDaysAgo.AddHours(12) // two days ago afternoon
        };

        // User 4: Never active but has streak > 0 (edge case) - streak SHOULD reset
        var userNeverActive = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_never",
            Email = "never@example.com",
            Streak = 2,
            LastAttemptDateUtc = null
        };

        context.Users.AddRange(userActiveToday, userActiveYesterday, userActiveTwoDaysAgo, userNeverActive);
        await context.SaveChangesAsync();

        var leaderboardServiceMock = new Mock<ILeaderboardService>();
        var job = new NightlyMaintenanceJob(context, leaderboardServiceMock.Object);

        // Act
        await job.RunNightlyMaintenanceAsync();

        // Assert
        using var assertContext = CreateDbContext(dbName);
        
        var dbUserToday = await assertContext.Users.FindAsync(userActiveToday.Id);
        dbUserToday!.Streak.Should().Be(3); // untouched

        var dbUserYesterday = await assertContext.Users.FindAsync(userActiveYesterday.Id);
        dbUserYesterday!.Streak.Should().Be(5); // untouched

        var dbUserTwoDaysAgo = await assertContext.Users.FindAsync(userActiveTwoDaysAgo.Id);
        dbUserTwoDaysAgo!.Streak.Should().Be(0); // reset to 0

        var dbUserNever = await assertContext.Users.FindAsync(userNeverActive.Id);
        dbUserNever!.Streak.Should().Be(0); // reset to 0

        // Verify cache recomputation was called
        leaderboardServiceMock.Verify(s => s.RecomputeLeaderboardCacheAsync(), Times.Once);
    }
}
