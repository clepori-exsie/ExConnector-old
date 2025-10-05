using ExConnector.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExConnector.API.HealthChecks;

/// <summary>
/// Health check pour vérifier que la configuration est valide
/// </summary>
public class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public ConfigurationHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var data = new Dictionary<string, object>();

        // Vérifier API Key
        var apiKey = _configuration["Api:ApiKey"];
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(apiKey);
        data["apiKeyConfigured"] = apiKeyConfigured;
        if (!apiKeyConfigured)
            issues.Add("Clé API non configurée");

        // Vérifier Admin
        var adminUser = _configuration["Admin:User"];
        var adminPasswordHash = _configuration["Admin:PasswordHash"];
        var adminConfigured = !string.IsNullOrWhiteSpace(adminUser) && !string.IsNullOrWhiteSpace(adminPasswordHash);
        data["adminConfigured"] = adminConfigured;
        if (!adminConfigured)
            issues.Add("Utilisateur admin non configuré correctement");

        // Vérifier Sage Config
        var sageConfig = _configuration.GetSection("SageConfig").Get<SageConfig>();
        var sageConfigured = sageConfig != null && (
            !string.IsNullOrWhiteSpace(sageConfig.MAEPath) ||
            (!string.IsNullOrWhiteSpace(sageConfig.CompanyServer) && !string.IsNullOrWhiteSpace(sageConfig.CompanyDatabaseName))
        );
        data["sageConfigured"] = sageConfigured;
        if (!sageConfigured)
            issues.Add("Configuration Sage 100 incomplète");

        // Résultat
        if (issues.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Configuration complète et valide", data));
        }

        if (issues.Count <= 1)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Configuration partielle : {string.Join(", ", issues)}",
                data: data
            ));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Configuration invalide : {string.Join(", ", issues)}",
            data: data
        ));
    }
}

