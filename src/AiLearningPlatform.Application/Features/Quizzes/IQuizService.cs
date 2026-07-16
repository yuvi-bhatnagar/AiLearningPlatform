using AiLearningPlatform.Application.Features.Quizzes.DTOs;

namespace AiLearningPlatform.Application.Features.Quizzes;

public interface IQuizService
{
    Task<QuizDto> GetByIdAsync(Guid id);
    Task<IEnumerable<QuizDto>> GetByCourseIdAsync(Guid courseId);
    Task<QuizDto> CreateAsync(CreateQuizRequest request, Guid currentUserId, string currentUserRole);
    Task<QuizDto> UpdateAsync(Guid id, UpdateQuizRequest request, Guid currentUserId, string currentUserRole);
    Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole);
}
