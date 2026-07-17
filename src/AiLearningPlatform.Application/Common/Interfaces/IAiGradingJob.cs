namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IAiGradingJob
{
    Task GradeSubjectiveAnswersAsync(Guid attemptId);
}
