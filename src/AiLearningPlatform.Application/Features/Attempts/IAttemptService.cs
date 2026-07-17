using AiLearningPlatform.Application.Features.Attempts.DTOs;

namespace AiLearningPlatform.Application.Features.Attempts;

public interface IAttemptService
{
    Task<AttemptDto> StartAttemptAsync(Guid quizId, Guid userId);
    Task<AttemptResultDto> SubmitAttemptAsync(Guid attemptId, SubmitAttemptRequest request, Guid userId);
    Task<AttemptResultDto> GetAttemptByIdAsync(Guid attemptId, Guid userId, string userRole);
    Task<IEnumerable<AttemptResultDto>> GetAttemptsByUserIdAsync(Guid userId);
    Task<AttemptResultDto> OverrideSubmissionGradeAsync(Guid attemptId, OverrideGradeRequest request, Guid teacherId);
    Task<IEnumerable<TeacherReviewDto>> GetLowConfidenceReviewsAsync(Guid teacherId);
}
