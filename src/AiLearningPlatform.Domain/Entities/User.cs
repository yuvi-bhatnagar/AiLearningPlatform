using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Refresh token stored here; null means no active session
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryUtc { get; set; }

    // Navigation properties
    public ICollection<Course> AuthoredCourses { get; set; } = new List<Course>();
    public ICollection<Attempt> QuizAttempts { get; set; } = new List<Attempt>();
}
