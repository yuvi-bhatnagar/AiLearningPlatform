using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IQuestionRepository
{
    Task<Question?> GetByIdAsync(Guid id);
    Task<IEnumerable<Question>> GetByQuizIdAsync(Guid quizId);
    Task AddAsync(Question question);
    void Update(Question question);
    void Delete(Question question);
    Task SaveChangesAsync();
}
