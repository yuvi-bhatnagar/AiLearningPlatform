using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Questions.DTOs;

public record UpdateQuestionRequest(
    string Text,
    QuestionType Type,
    List<string> Options,
    string CorrectAnswer,
    int Points
);
