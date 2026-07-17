using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Hangfire;

namespace AiLearningPlatform.Infrastructure.BackgroundJobs;

public class HangfireHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using (var connection = JobStorage.Current.GetConnection())
            {
                return Task.FromResult(HealthCheckResult.Healthy("Hangfire storage connection is active."));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire storage connection is unhealthy.", ex));
        }
    }
}
