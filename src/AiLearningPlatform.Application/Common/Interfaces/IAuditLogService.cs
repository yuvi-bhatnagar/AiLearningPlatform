using System.Threading.Tasks;

namespace AiLearningPlatform.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogActionAsync(string action, string details);
}
