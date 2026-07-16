using AiLearningPlatform.Application.Features.Courses.DTOs;

namespace AiLearningPlatform.Application.Features.Courses;

public interface ICourseService
{
    Task<CourseDto> GetByIdAsync(Guid id);
    Task<IEnumerable<CourseDto>> GetAllAsync();
    Task<CourseDto> CreateAsync(CreateCourseRequest request, Guid instructorId);
    Task<CourseDto> UpdateAsync(Guid id, UpdateCourseRequest request, Guid currentUserId, string currentUserRole);
    Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole);
}
