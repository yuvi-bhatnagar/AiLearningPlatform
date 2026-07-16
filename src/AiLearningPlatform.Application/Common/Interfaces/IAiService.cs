using AiLearningPlatform.Application.Features.AI.DTOs;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IAiService
{
    Task<List<GeneratedQuestionDto>> GenerateQuizAsync(string topic, int questionCount);
    Task<AiEvaluationResultDto> EvaluateAnswerAsync(string questionText, string correctAnswerKeywords, string studentAnswer);
}
