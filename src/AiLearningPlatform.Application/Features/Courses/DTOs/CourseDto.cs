namespace AiLearningPlatform.Application.Features.Courses.DTOs;

public record CourseDto(
    Guid Id,
    string Title,
    string Description,
    Guid InstructorId,
    DateTime CreatedAtUtc
);
