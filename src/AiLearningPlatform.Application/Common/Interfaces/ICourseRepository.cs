using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id);
    Task<IEnumerable<Course>> GetAllAsync();
    Task AddAsync(Course course);
    void Update(Course course);
    void Delete(Course course);
    Task SaveChangesAsync();
}
