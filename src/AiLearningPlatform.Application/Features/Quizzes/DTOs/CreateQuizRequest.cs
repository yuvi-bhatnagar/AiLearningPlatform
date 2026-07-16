namespace AiLearningPlatform.Application.Features.Quizzes.DTOs;

public record CreateQuizRequest(
    string Title,
    string Description,
    Guid CourseId
);
