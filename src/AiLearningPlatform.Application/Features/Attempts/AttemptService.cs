using System.Text.Json;
using FluentValidation;
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
    private readonly IValidator<StartAttemptRequest> _startValidator;
    private readonly IValidator<SubmitAttemptRequest> _submitValidator;

    public AttemptService(
        IAttemptRepository attemptRepository,
        IQuizRepository quizRepository,
        ICourseRepository courseRepository,
        IValidator<StartAttemptRequest> startValidator,
        IValidator<SubmitAttemptRequest> submitValidator)
    {
        _attemptRepository = attemptRepository;
        _quizRepository = quizRepository;
        _courseRepository = courseRepository;
        _startValidator = startValidator;
        _submitValidator = submitValidator;
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

        await _attemptRepository.SaveChangesAsync();

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
            ans.Feedback
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
