using AiLearningPlatform.Application.Features.Questions.DTOs;

namespace AiLearningPlatform.Application.Features.Questions;

public interface IQuestionService
{
    Task<QuestionDto> GetByIdAsync(Guid id);
    Task<IEnumerable<QuestionDto>> GetByQuizIdAsync(Guid quizId);
    Task<QuestionDto> CreateAsync(CreateQuestionRequest request, Guid currentUserId, string currentUserRole);
    Task<QuestionDto> UpdateAsync(Guid id, UpdateQuestionRequest request, Guid currentUserId, string currentUserRole);
    Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole);
}
