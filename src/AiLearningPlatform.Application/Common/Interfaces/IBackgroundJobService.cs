namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueGradingJob(Guid attemptId);
}
