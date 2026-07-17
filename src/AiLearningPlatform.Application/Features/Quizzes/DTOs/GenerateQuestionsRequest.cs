namespace AiLearningPlatform.Application.Features.Quizzes.DTOs;

public record GenerateQuestionsRequest(
    string Topic,
    int QuestionCount
);
