namespace AiLearningPlatform.Application.Features.Quizzes.DTOs;

public record QuizDto(
    Guid Id,
    string Title,
    string Description,
    Guid CourseId,
    DateTime CreatedAtUtc
);
