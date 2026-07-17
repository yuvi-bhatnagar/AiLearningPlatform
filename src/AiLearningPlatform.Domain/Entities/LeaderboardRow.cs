namespace AiLearningPlatform.Domain.Entities;

public class LeaderboardRow
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public double TotalScore { get; set; }
    public int QuizzesAttempted { get; set; }
    public int Rank { get; set; }
}
