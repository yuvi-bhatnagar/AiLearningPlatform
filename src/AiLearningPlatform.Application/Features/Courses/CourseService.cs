using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Features.Courses;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IValidator<CreateCourseRequest> _createValidator;
    private readonly IValidator<UpdateCourseRequest> _updateValidator;

    public CourseService(
        ICourseRepository courseRepository,
        IValidator<CreateCourseRequest> createValidator,
        IValidator<UpdateCourseRequest> updateValidator)
    {
        _courseRepository = courseRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CourseDto> GetByIdAsync(Guid id)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course is null)
            throw new NotFoundException(nameof(Course), id);

        return MapToDto(course);
    }

    public async Task<IEnumerable<CourseDto>> GetAllAsync()
    {
        var courses = await _courseRepository.GetAllAsync();
        return courses.Select(MapToDto);
    }

    public async Task<CourseDto> CreateAsync(CreateCourseRequest request, Guid instructorId)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            InstructorId = instructorId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _courseRepository.AddAsync(course);
        await _courseRepository.SaveChangesAsync();

        return MapToDto(course);
    }

    public async Task<CourseDto> UpdateAsync(Guid id, UpdateCourseRequest request, Guid currentUserId, string currentUserRole)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var course = await _courseRepository.GetByIdAsync(id);
        if (course is null)
            throw new NotFoundException(nameof(Course), id);

        // Authorization: Only authoring Teacher or Admin can update
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to update this course.");

        course.Title = request.Title;
        course.Description = request.Description;

        _courseRepository.Update(course);
        await _courseRepository.SaveChangesAsync();

        return MapToDto(course);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course is null)
            throw new NotFoundException(nameof(Course), id);

        // Authorization: Only authoring Teacher or Admin can delete
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to delete this course.");

        _courseRepository.Delete(course);
        await _courseRepository.SaveChangesAsync();
    }

    private static CourseDto MapToDto(Course course) =>
        new(course.Id, course.Title, course.Description, course.InstructorId, course.CreatedAtUtc);

    private static IDictionary<string, string[]> ToDictionary(FluentValidation.Results.ValidationResult result) =>
        result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
