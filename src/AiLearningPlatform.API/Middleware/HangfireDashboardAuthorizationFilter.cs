using Hangfire.Dashboard;
using System.Security.Claims;
using System.Net;

namespace AiLearningPlatform.API.Middleware;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // 1. Allow local/loopback requests for local testing convenience
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp != null && (IPAddress.IsLoopback(remoteIp) || remoteIp.Equals(httpContext.Connection.LocalIpAddress)))
        {
            return true;
        }

        // 2. Otherwise require authenticated Teacher or Admin
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            if (role == "Teacher" || role == "Admin")
            {
                return true;
            }
        }

        return false;
    }
}
