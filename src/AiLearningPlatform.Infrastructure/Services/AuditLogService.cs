using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogActionAsync(string action, string details)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        Guid? userId = null;

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var parsedId))
            {
                userId = parsedId;
            }
        }

        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = details,
            IpAddress = ipAddress,
            TimestampUtc = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
