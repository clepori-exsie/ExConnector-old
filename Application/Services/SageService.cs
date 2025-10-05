using ExConnector.Core.DTOs;
using ExConnector.Core.Interfaces;
using ExConnector.Infrastructure.Sage;
using ExConnector.Models;
using Microsoft.Extensions.Logging;

namespace ExConnector.Application.Services;

/// <summary>
/// Service de gestion des opérations Sage
/// </summary>
public class SageService : ISageService
{
    private readonly ILogger<SageService> _logger;
    private readonly ISageConnectionFactory _connectionFactory;

    public SageService(
        ILogger<SageService> logger,
        ISageConnectionFactory connectionFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public Task<ServiceResult> PingAsync()
    {
        return StaRunner.RunAsync(() =>
        {
            using var connection = _connectionFactory.CreateConnection();
            try
            {
                connection.Open();
                return connection.IsOpen
                    ? new ServiceResult(true, "Base comptable ouverte avec succès (ping).")
                    : new ServiceResult(false, "Open() n'a pas reporté IsOpen=true.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur Ping Sage");
                return new ServiceResult(false, "Erreur : " + ex.Message);
            }
        });
    }

    public Task<ServiceResult> TestConnectionAsync(SageConfig config)
    {
        return StaRunner.RunAsync(() =>
        {
            using var connection = _connectionFactory.CreateConnection(config);
            try
            {
                connection.Open();
                return connection.IsOpen
                    ? new ServiceResult(true, "Connexion réussie à la base Sage !")
                    : new ServiceResult(false, "Échec : Open() n'a pas retourné IsOpen=true.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur Test Connexion Sage");
                return new ServiceResult(false, "Erreur : " + ex.Message);
            }
        });
    }
}

