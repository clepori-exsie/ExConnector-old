using ExConnector.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExConnector.API.HealthChecks;

/// <summary>
/// Health check pour vérifier la connexion à Sage 100
/// </summary>
public class SageHealthCheck : IHealthCheck
{
    private readonly ISageService _sageService;

    public SageHealthCheck(ISageService sageService)
    {
        _sageService = sageService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _sageService.PingAsync();

            if (result.Success)
            {
                return HealthCheckResult.Healthy(
                    "Connexion Sage 100 fonctionnelle",
                    new Dictionary<string, object>
                    {
                        ["message"] = result.Message,
                        ["timestamp"] = DateTime.UtcNow
                    }
                );
            }

            return HealthCheckResult.Degraded(
                "Sage 100 accessible mais problème de connexion",
                data: new Dictionary<string, object>
                {
                    ["error"] = result.Message,
                    ["timestamp"] = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Impossible de se connecter à Sage 100",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["timestamp"] = DateTime.UtcNow
                }
            );
        }
    }
}

