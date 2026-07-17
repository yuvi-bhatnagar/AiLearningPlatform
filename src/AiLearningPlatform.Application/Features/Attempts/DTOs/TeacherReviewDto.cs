using System;

namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record TeacherReviewDto(
    Guid AttemptId,
    Guid QuizId,
    string StudentName,
    string QuizTitle,
    Guid QuestionId,
    string QuestionText,
    string StudentAnswer,
    string CorrectAnswer,
    double? Score,
    double MaxPoints,
    string? Feedback,
    string? Confidence
);
