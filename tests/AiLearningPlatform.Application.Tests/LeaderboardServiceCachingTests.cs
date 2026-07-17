using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using AiLearningPlatform.Application.Features.Leaderboards.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;
using AiLearningPlatform.Infrastructure.Services;

namespace AiLearningPlatform.Application.Tests;

public class LeaderboardServiceCachingTests
{
    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ShouldReturnFromCache_WhenCacheExists()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        var cacheMock = new Mock<IDistributedCache>();
        var cachedData = new List<LeaderboardRowDto>
        {
            new(Guid.NewGuid(), "CachedStudent", 100.0, 5, 1)
        };
        var json = JsonSerializer.Serialize(cachedData);
        var bytes = Encoding.UTF8.GetBytes(json);

        cacheMock.Setup(c => c.GetAsync("LeaderboardData", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var service = new LeaderboardService(context, cacheMock.Object);

        // Act
        var result = await service.GetLeaderboardAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Username.Should().Be("CachedStudent");

        cacheMock.Verify(c => c.GetAsync("LeaderboardData", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ShouldFetchFromDbAndSetCache_WhenCacheIsEmpty()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);

        // Seed a user
        var user = new User { Id = Guid.NewGuid(), Username = "db_student", Email = "db@edu.com", Role = Domain.Enums.UserRole.Student, PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync("LeaderboardData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        cacheMock.Setup(c => c.SetAsync(
            "LeaderboardData",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new LeaderboardService(context, cacheMock.Object);

        // Act
        var result = await service.GetLeaderboardAsync();

        // Assert
        result.Should().NotBeNull();
        // Since it's keyless view and InMemoryDatabase doesn't support raw SQL views or keyless queries easily,
        // it may return empty, but it should query DB and call SetAsync on the cache
        cacheMock.Verify(c => c.GetAsync("LeaderboardData", It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.SetAsync(
            "LeaderboardData",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
