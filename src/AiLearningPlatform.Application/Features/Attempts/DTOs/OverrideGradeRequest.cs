using System;

namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record OverrideGradeRequest(
    Guid QuestionId,
    double Score,
    string Feedback
);
