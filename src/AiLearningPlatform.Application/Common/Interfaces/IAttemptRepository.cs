using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IAttemptRepository
{
    Task<Attempt?> GetByIdAsync(Guid id);
    Task<IEnumerable<Attempt>> GetByUserIdAsync(Guid userId);
    Task<Attempt?> GetActiveAttemptAsync(Guid quizId, Guid userId);
    Task AddAsync(Attempt attempt);
    void Update(Attempt attempt);
    Task SaveChangesAsync();
}
