namespace AiLearningPlatform.Domain.Entities;

public class StudentPerformanceSummary
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public double TotalScore { get; set; }
    public double AverageScore { get; set; }
    public int TotalAttempts { get; set; }
    public double HighestScore { get; set; }
    public double LowestScore { get; set; }
}
