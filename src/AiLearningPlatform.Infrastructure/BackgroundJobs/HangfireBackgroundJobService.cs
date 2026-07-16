using Hangfire;
using AiLearningPlatform.Application.Common.Interfaces;

namespace AiLearningPlatform.Infrastructure.BackgroundJobs;

public class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireBackgroundJobService(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueueGradingJob(Guid attemptId)
    {
        _backgroundJobClient.Enqueue<IAiGradingJob>(job => job.GradeSubjectiveAnswersAsync(attemptId));
    }
}
