using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record AttemptQuestionDto(
    Guid Id,
    string Text,
    QuestionType Type,
    List<string> Options,
    int Points
);
