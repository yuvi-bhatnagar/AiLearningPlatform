namespace AiLearningPlatform.Application.Features.AI.DTOs;

public record GeneratedQuestionDto(
    string Text,
    List<string> Options,
    string CorrectAnswer,
    int Points
);
