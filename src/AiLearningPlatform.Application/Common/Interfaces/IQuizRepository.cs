using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IQuizRepository
{
    Task<Quiz?> GetByIdAsync(Guid id);
    Task<IEnumerable<Quiz>> GetByCourseIdAsync(Guid courseId);
    Task AddAsync(Quiz quiz);
    void Update(Quiz quiz);
    void Delete(Quiz quiz);
    Task SaveChangesAsync();
}
