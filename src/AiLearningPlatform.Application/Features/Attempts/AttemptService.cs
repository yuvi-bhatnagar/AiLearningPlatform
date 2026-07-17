using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Attempts.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Features.Attempts;

public class AttemptService : IAttemptService
{
    private readonly IAttemptRepository _attemptRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IValidator<StartAttemptRequest> _startValidator;
    private readonly IValidator<SubmitAttemptRequest> _submitValidator;
    private readonly IDistributedCache _cache;
    private readonly IAuditLogService _auditLogService;

    public AttemptService(
        IAttemptRepository attemptRepository,
        IQuizRepository quizRepository,
        ICourseRepository courseRepository,
        IBackgroundJobService backgroundJobService,
        IValidator<StartAttemptRequest> startValidator,
        IValidator<SubmitAttemptRequest> submitValidator,
        IDistributedCache cache,
        IAuditLogService auditLogService)
    {
        _attemptRepository = attemptRepository;
        _quizRepository = quizRepository;
        _courseRepository = courseRepository;
        _backgroundJobService = backgroundJobService;
        _startValidator = startValidator;
        _submitValidator = submitValidator;
        _cache = cache;
        _auditLogService = auditLogService;
    }

    public async Task<AttemptDto> StartAttemptAsync(Guid quizId, Guid userId)
    {
        var request = new StartAttemptRequest(quizId);
        var validationResult = await _startValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var quiz = await _quizRepository.GetByIdAsync(quizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), quizId);

        // Check for active attempt in progress
        var activeAttempt = await _attemptRepository.GetActiveAttemptAsync(quizId, userId);
        if (activeAttempt is not null)
        {
            var errors = new Dictionary<string, string[]>
            {
                { "QuizId", new[] { "You already have an active attempt in progress for this quiz." } }
            };
            throw new Domain.Exceptions.ValidationException(errors);
        }

        var attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            QuizId = quizId,
            UserId = userId,
            StartedAtUtc = DateTime.UtcNow,
            Status = AttemptStatus.InProgress,
            Quiz = quiz
        };

        await _attemptRepository.AddAsync(attempt);
        await _attemptRepository.SaveChangesAsync();

        await _auditLogService.LogActionAsync("QuizStart", $"Attempt {attempt.Id} started for Quiz {quizId}.");

        return MapToAttemptDto(attempt);
    }

    public async Task<AttemptResultDto> SubmitAttemptAsync(Guid attemptId, SubmitAttemptRequest request, Guid userId)
    {
        var validationResult = await _submitValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var attempt = await _attemptRepository.GetByIdAsync(attemptId);
        if (attempt is null)
            throw new NotFoundException(nameof(Attempt), attemptId);

        if (attempt.UserId != userId)
            throw new UnauthorizedAccessException("You are not authorized to submit this attempt.");

        if (attempt.Status != AttemptStatus.InProgress)
        {
            var errors = new Dictionary<string, string[]>
            {
                { "Status", new[] { "Attempt is already submitted or graded." } }
            };
            throw new Domain.Exceptions.ValidationException(errors);
        }

        var questions = attempt.Quiz.Questions;
        var submittedAnswersMap = request.Answers.ToDictionary(a => a.QuestionId, a => a.StudentAnswer);

        // Validate that all submitted answers belong to the quiz questions
        foreach (var ans in request.Answers)
        {
            if (!questions.Any(q => q.Id == ans.QuestionId))
            {
                var errors = new Dictionary<string, string[]>
                {
                    { "Answers", new[] { $"Question with ID {ans.QuestionId} does not belong to this quiz." } }
                };
                throw new Domain.Exceptions.ValidationException(errors);
            }
        }

        attempt.AnswerSubmissions.Clear();

        double totalScore = 0;
        bool hasSubjective = false;

        foreach (var question in questions)
        {
            submittedAnswersMap.TryGetValue(question.Id, out var studentAnswer);
            studentAnswer = studentAnswer?.Trim() ?? string.Empty;

            var submission = new AnswerSubmission
            {
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                StudentAnswer = studentAnswer
            };

            if (question.Type == QuestionType.MultipleChoice)
            {
                var isCorrect = string.Equals(studentAnswer, question.CorrectAnswer?.Trim(), StringComparison.OrdinalIgnoreCase);
                submission.IsCorrect = isCorrect;
                submission.Score = isCorrect ? question.Points : 0;
                totalScore += submission.Score.Value;
            }
            else if (question.Type == QuestionType.Subjective)
            {
                submission.IsCorrect = null;
                submission.Score = null;
                submission.Feedback = null;
                hasSubjective = true;
            }

            attempt.AnswerSubmissions.Add(submission);
        }

        attempt.SubmittedAtUtc = DateTime.UtcNow;
        if (hasSubjective)
        {
            attempt.Status = AttemptStatus.PendingGrading;
            attempt.Score = null;
        }
        else
        {
            attempt.Status = AttemptStatus.Graded;
            attempt.Score = totalScore;
        }

        // Update student streak
        var user = await _attemptRepository.GetUserByIdAsync(userId);
        if (user != null)
        {
            var now = DateTime.UtcNow;
            if (user.LastAttemptDateUtc.HasValue)
            {
                var lastDate = user.LastAttemptDateUtc.Value.Date;
                var todayDate = now.Date;
                var yesterdayDate = todayDate.AddDays(-1);

                if (lastDate == yesterdayDate)
                {
                    user.Streak += 1;
                }
                else if (lastDate < yesterdayDate)
                {
                    user.Streak = 1;
                }
                // If lastDate == todayDate, do nothing (streak is already updated today)
            }
            else
            {
                user.Streak = 1;
            }
            user.LastAttemptDateUtc = now;
        }

        await _attemptRepository.SaveChangesAsync();

        await _auditLogService.LogActionAsync("QuizSubmit", $"Attempt {attempt.Id} submitted. HasSubjective: {hasSubjective}.");

        if (hasSubjective)
        {
            _backgroundJobService.EnqueueGradingJob(attempt.Id);
        }
        else
        {
            await _cache.RemoveAsync("LeaderboardData");
        }

        return MapToResultDto(attempt);
    }

    public async Task<AttemptResultDto> GetAttemptByIdAsync(Guid attemptId, Guid userId, string userRole)
    {
        var attempt = await _attemptRepository.GetByIdAsync(attemptId);
        if (attempt is null)
            throw new NotFoundException(nameof(Attempt), attemptId);

        // Security check: Only the student who started the attempt, the course teacher, or an Admin can view it
        if (userRole != "Admin" && attempt.UserId != userId)
        {
            var course = await _courseRepository.GetByIdAsync(attempt.Quiz.CourseId);
            if (course is null || course.InstructorId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to view this attempt.");
            }
        }

        return MapToResultDto(attempt);
    }

    public async Task<IEnumerable<AttemptResultDto>> GetAttemptsByUserIdAsync(Guid userId)
    {
        var attempts = await _attemptRepository.GetByUserIdAsync(userId);
        return attempts.Select(MapToResultDto).ToList();
    }

    public async Task<AttemptResultDto> OverrideSubmissionGradeAsync(Guid attemptId, OverrideGradeRequest request, Guid teacherId)
    {
        var attempt = await _attemptRepository.GetByIdAsync(attemptId);
        if (attempt is null)
            throw new NotFoundException(nameof(Attempt), attemptId);

        // Security check: Only the course teacher can override grades
        var course = await _courseRepository.GetByIdAsync(attempt.Quiz.CourseId);
        if (course is null || course.InstructorId != teacherId)
        {
            throw new UnauthorizedAccessException("Only the instructor of the course can override grades.");
        }

        var submission = attempt.AnswerSubmissions.FirstOrDefault(s => s.QuestionId == request.QuestionId);
        if (submission is null)
            throw new NotFoundException(nameof(AnswerSubmission), request.QuestionId);

        submission.Score = request.Score;
        submission.Feedback = request.Feedback;
        submission.IsCorrect = request.Score >= (submission.Question.Points / 2.0);
        submission.Confidence = "ManualOverride";

        // Recalculate total score
        attempt.Score = attempt.AnswerSubmissions.Sum(s => s.Score ?? 0);

        await _attemptRepository.SaveChangesAsync();
        await _cache.RemoveAsync("LeaderboardData");

        return MapToResultDto(attempt);
    }

    public async Task<IEnumerable<TeacherReviewDto>> GetLowConfidenceReviewsAsync(Guid teacherId)
    {
        var attempts = await _attemptRepository.GetLowConfidenceAttemptsByInstructorAsync(teacherId);
        var reviews = new List<TeacherReviewDto>();

        foreach (var attempt in attempts)
        {
            var studentName = attempt.User?.Username ?? "Unknown";
            var quizTitle = attempt.Quiz?.Title ?? "Unknown";

            foreach (var sub in attempt.AnswerSubmissions)
            {
                if (sub.Confidence == "Low")
                {
                    reviews.Add(new TeacherReviewDto(
                        attempt.Id,
                        attempt.QuizId,
                        studentName,
                        quizTitle,
                        sub.QuestionId,
                        sub.Question?.Text ?? "Unknown",
                        sub.StudentAnswer,
                        sub.Question?.CorrectAnswer ?? "Unknown",
                        sub.Score,
                        sub.Question?.Points ?? 0,
                        sub.Feedback,
                        sub.Confidence
                    ));
                }
            }
        }

        return reviews;
    }

    private static AttemptDto MapToAttemptDto(Attempt attempt)
    {
        var questions = attempt.Quiz.Questions.Select(q =>
        {
            List<string> options;
            try
            {
                options = string.IsNullOrEmpty(q.OptionsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? new List<string>();
            }
            catch
            {
                options = new List<string>();
            }
            return new AttemptQuestionDto(q.Id, q.Text, q.Type, options, q.Points);
        }).ToList();

        return new AttemptDto(
            attempt.Id,
            attempt.QuizId,
            attempt.UserId,
            attempt.StartedAtUtc,
            attempt.Status.ToString(),
            questions
        );
    }

    private static AttemptResultDto MapToResultDto(Attempt attempt)
    {
        var submissions = attempt.AnswerSubmissions.Select(ans => new AnswerSubmissionDto(
            ans.QuestionId,
            ans.StudentAnswer,
            ans.IsCorrect,
            ans.Score,
            ans.Feedback,
            ans.Confidence
        )).ToList();

        return new AttemptResultDto(
            attempt.Id,
            attempt.QuizId,
            attempt.UserId,
            attempt.StartedAtUtc,
            attempt.SubmittedAtUtc,
            attempt.Score,
            attempt.Status.ToString(),
            submissions
        );
    }

    private static IDictionary<string, string[]> ToDictionary(FluentValidation.Results.ValidationResult result) =>
        result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
