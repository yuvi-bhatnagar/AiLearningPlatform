using System;

namespace AiLearningPlatform.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }

    // Navigation property
    public User? User { get; set; }
}
