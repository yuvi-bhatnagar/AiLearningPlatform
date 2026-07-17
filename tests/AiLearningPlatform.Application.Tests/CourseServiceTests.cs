using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Courses;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using AiLearningPlatform.Application.Features.Courses.Validators;
using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Tests;

public class CourseServiceTests
{
    private readonly Mock<ICourseRepository> _courseRepoMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly CourseService _courseService;

    public CourseServiceTests()
    {
        var createValidator = new CreateCourseRequestValidator();
        var updateValidator = new UpdateCourseRequestValidator();

        _courseService = new CourseService(
            _courseRepoMock.Object,
            createValidator,
            updateValidator,
            _cacheMock.Object
        );
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCallRepository_AndInvalidateCache()
    {
        // Arrange
        var request = new CreateCourseRequest("Introduction to C#", "Learn basic C# features");
        var instructorId = Guid.NewGuid();

        _courseRepoMock.Setup(repo => repo.AddAsync(It.IsAny<Course>()))
            .Returns(Task.CompletedTask);
        _courseRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _courseService.CreateAsync(request, instructorId);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(request.Title);

        _courseRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Course>()), Times.Once);
        _courseRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync("CourseListData", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnFromCache_WhenCacheExists()
    {
        // Arrange
        var cachedCourses = new List<CourseDto>
        {
            new(Guid.NewGuid(), "Cached Course 1", "Desc 1", Guid.NewGuid(), DateTime.UtcNow)
        };
        var json = JsonSerializer.Serialize(cachedCourses);
        var bytes = Encoding.UTF8.GetBytes(json);

        _cacheMock.Setup(c => c.GetAsync("CourseListData", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _courseService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Cached Course 1");

        _courseRepoMock.Verify(repo => repo.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFetchFromDbAndSetCache_WhenCacheIsEmpty()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetAsync("CourseListData", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var dbCourses = new List<Course>
        {
            new() { Id = Guid.NewGuid(), Title = "DB Course 1", Description = "Desc", InstructorId = Guid.NewGuid() }
        };

        _courseRepoMock.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(dbCourses);

        _cacheMock.Setup(c => c.SetAsync(
            "CourseListData",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _courseService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("DB Course 1");

        _courseRepoMock.Verify(repo => repo.GetAllAsync(), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            "CourseListData",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTitle_ShouldThrowValidationException()
    {
        // Arrange
        var request = new CreateCourseRequest("", "Valid description");
        var instructorId = Guid.NewGuid();

        // Act
        var act = () => _courseService.CreateAsync(request, instructorId);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.ValidationException>()
            .Where(e => e.Errors.ContainsKey("Title"));

        _courseRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Course>()), Times.Never);
        _courseRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }
}
