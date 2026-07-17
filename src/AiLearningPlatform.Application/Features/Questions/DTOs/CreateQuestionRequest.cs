using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Questions.DTOs;

public record CreateQuestionRequest(
    Guid QuizId,
    string Text,
    QuestionType Type,
    List<string> Options,
    string CorrectAnswer,
    int Points
);
