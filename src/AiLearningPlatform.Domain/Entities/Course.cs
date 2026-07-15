namespace AiLearningPlatform.Domain.Entities;

public class Course
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid InstructorId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User Instructor { get; set; } = null!;
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
