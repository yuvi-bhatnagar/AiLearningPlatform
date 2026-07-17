using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AnswerSubmission> AnswerSubmissions => Set<AnswerSubmission>();
    public DbSet<LeaderboardRow> Leaderboard => Set<LeaderboardRow>();
    public DbSet<StudentPerformanceSummary> StudentPerformanceSummaries => Set<StudentPerformanceSummary>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User Entity Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            
            entity.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // 2. Course Entity Configuration
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(1000);

            // Instructor (User) -> AuthoredCourses (1-to-many)
            entity.HasOne(c => c.Instructor)
                .WithMany(u => u.AuthoredCourses)
                .HasForeignKey(c => c.InstructorId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting instructor if they have courses
        });

        // 3. Quiz Entity Configuration
        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Title).IsRequired().HasMaxLength(100);
            entity.Property(q => q.Description).HasMaxLength(1000);

            // Course -> Quizzes (1-to-many)
            entity.HasOne(q => q.Course)
                .WithMany(c => c.Quizzes)
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.Cascade); // Delete quizzes if course is deleted
        });

        // 4. Question Entity Configuration
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Text).IsRequired().HasMaxLength(2000);
            entity.Property(q => q.CorrectAnswer).IsRequired().HasMaxLength(1000);
            
            entity.Property(q => q.Type)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Quiz -> Questions (1-to-many)
            entity.HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade); // Delete questions if quiz is deleted
        });

        // 5. Attempt Entity Configuration
        modelBuilder.Entity<Attempt>(entity =>
        {
            entity.HasKey(a => a.Id);
            
            entity.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.QuizId);

            // Quiz -> Attempts (1-to-many)
            entity.HasOne(a => a.Quiz)
                .WithMany(q => q.Attempts)
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.Restrict); // Avoid multiple cascade paths

            // User -> Attempts (1-to-many)
            entity.HasOne(a => a.User)
                .WithMany(u => u.QuizAttempts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Delete attempts if user is deleted
        });

        // 6. AnswerSubmission Entity Configuration
        modelBuilder.Entity<AnswerSubmission>(entity =>
        {
            entity.HasKey(asub => asub.Id);
            entity.Property(asub => asub.StudentAnswer).IsRequired(); // Could be long text, no max length
            entity.Property(asub => asub.Feedback).HasMaxLength(2000);

            // Attempt -> AnswerSubmissions (1-to-many)
            entity.HasOne(asub => asub.Attempt)
                .WithMany(a => a.AnswerSubmissions)
                .HasForeignKey(asub => asub.AttemptId)
                .OnDelete(DeleteBehavior.Cascade); // Delete answers if attempt is deleted

            // Question -> AnswerSubmissions (1-to-many)
            entity.HasOne(asub => asub.Question)
                .WithMany(q => q.AnswerSubmissions)
                .HasForeignKey(asub => asub.QuestionId)
                .OnDelete(DeleteBehavior.Restrict); // Avoid multiple cascade paths
        });

        // 7. LeaderboardRow Configuration (Keyless Entity mapped to View)
        modelBuilder.Entity<LeaderboardRow>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("LeaderboardView");
        });

        // 8. StudentPerformanceSummary Configuration (Keyless Entity mapped to Stored Proc)
        modelBuilder.Entity<StudentPerformanceSummary>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable((string)null!);
        });

        // 9. AuditLog Configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(al => al.Id);
            entity.Property(al => al.Action).IsRequired().HasMaxLength(100);
            entity.Property(al => al.Details).HasMaxLength(4000);
            entity.Property(al => al.IpAddress).HasMaxLength(50);
            entity.Property(al => al.TimestampUtc).IsRequired();

            entity.HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Preserve logs if user is deleted
        });
    }
}
