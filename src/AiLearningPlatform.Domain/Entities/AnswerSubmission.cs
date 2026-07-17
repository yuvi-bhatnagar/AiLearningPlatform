namespace AiLearningPlatform.Domain.Entities;

public class AnswerSubmission
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public bool? IsCorrect { get; set; }
    public double? Score { get; set; }
    public string? Feedback { get; set; }
    public string? Confidence { get; set; }

    // Navigation properties
    public Attempt Attempt { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
