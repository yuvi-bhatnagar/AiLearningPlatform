namespace AiLearningPlatform.Application.Features.Attempts.DTOs;

public record SubmitAttemptRequest(
    List<SubmitAnswerDto> Answers
);
