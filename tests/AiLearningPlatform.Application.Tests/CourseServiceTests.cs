using FluentAssertions;
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
    private readonly CourseService _courseService;

    public CourseServiceTests()
    {
        // Concrete validators for testing actual validation execution inside the service
        var createValidator = new CreateCourseRequestValidator();
        var updateValidator = new UpdateCourseRequestValidator();

        _courseService = new CourseService(
            _courseRepoMock.Object,
            createValidator,
            updateValidator
        );
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCallRepositoryAndReturnDto()
    {
        // Arrange
        var request = new CreateCourseRequest("Introduction to C#", "Learn basic C# features");
        var instructorId = Guid.NewGuid();

        // Setup the mock to save successfully
        _courseRepoMock.Setup(repo => repo.AddAsync(It.IsAny<Course>()))
            .Returns(Task.CompletedTask);
        _courseRepoMock.Setup(repo => repo.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _courseService.CreateAsync(request, instructorId);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(request.Title);
        result.Description.Should().Be(request.Description);
        result.InstructorId.Should().Be(instructorId);

        // Verify repository methods were called exactly once
        _courseRepoMock.Verify(repo => repo.AddAsync(It.Is<Course>(c => 
            c.Title == request.Title && 
            c.Description == request.Description && 
            c.InstructorId == instructorId)), Times.Once);
        
        _courseRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyTitle_ShouldThrowValidationException()
    {
        // Arrange
        var request = new CreateCourseRequest("", "Valid description"); // Empty Title
        var instructorId = Guid.NewGuid();

        // Act
        var act = () => _courseService.CreateAsync(request, instructorId);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.ValidationException>()
            .Where(e => e.Errors.ContainsKey("Title"));

        // Verify repository was never called
        _courseRepoMock.Verify(repo => repo.AddAsync(It.IsAny<Course>()), Times.Never);
        _courseRepoMock.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }
}
