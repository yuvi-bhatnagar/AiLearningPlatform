using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Domain.Entities;

public class Attempt
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public double? Score { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;

    // Navigation properties
    public Quiz Quiz { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();
}
