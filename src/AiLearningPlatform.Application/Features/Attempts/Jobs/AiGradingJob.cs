using AiLearningPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Attempts.Jobs;

public class AiGradingJob : IAiGradingJob
{
    private readonly IAttemptRepository _attemptRepository;
    private readonly IAiService _aiService;
    private readonly IDistributedCache _cache;

    public AiGradingJob(IAttemptRepository attemptRepository, IAiService aiService, IDistributedCache cache)
    {
        _attemptRepository = attemptRepository;
        _aiService = aiService;
        _cache = cache;
    }

    public async Task GradeSubjectiveAnswersAsync(Guid attemptId)
    {
        var attempt = await _attemptRepository.GetByIdAsync(attemptId);
        if (attempt is null || attempt.Status != AttemptStatus.PendingGrading)
            return;

        var questions = attempt.Quiz.Questions;
        double totalScore = 0;

        foreach (var submission in attempt.AnswerSubmissions)
        {
            var question = questions.FirstOrDefault(q => q.Id == submission.QuestionId);
            if (question == null) continue;

            if (question.Type == QuestionType.MultipleChoice)
            {
                // MCQ score is already computed in SubmitAttemptAsync, so accumulate it
                totalScore += submission.Score ?? 0;
            }
            else if (question.Type == QuestionType.Subjective)
            {
                try
                {
                    var evalResult = await _aiService.EvaluateAnswerAsync(
                        question.Text,
                        question.CorrectAnswer ?? string.Empty,
                        submission.StudentAnswer);

                    // AI returns a score out of 10. Scale it to the question's Points.
                    var scaledScore = (evalResult.Score / 10.0) * question.Points;

                    submission.IsCorrect = evalResult.IsCorrect;
                    submission.Score = Math.Round(scaledScore, 2);
                    submission.Feedback = evalResult.Feedback;
                    submission.Confidence = evalResult.Confidence;
                    totalScore += submission.Score.Value;
                }
                catch (Exception)
                {
                    // Fallback to prevent background task failure loop
                    submission.IsCorrect = false;
                    submission.Score = 0;
                    submission.Feedback = "AI Evaluation failed. Please contact your instructor for manual grading.";
                }
            }
        }

        attempt.Status = AttemptStatus.Graded;
        attempt.Score = totalScore;

        await _attemptRepository.SaveChangesAsync();
        await _cache.RemoveAsync("LeaderboardData");
    }
}
