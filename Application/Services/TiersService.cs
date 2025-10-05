using ExConnector.Core.DTOs;
using ExConnector.Core.Interfaces;
using ExConnector.Infrastructure.Sage;
using Microsoft.Extensions.Logging;

namespace ExConnector.Application.Services;

/// <summary>
/// Service de gestion des tiers Sage - COM Dynamique
/// </summary>
public class TiersService : ITiersService
{
    private readonly ILogger<TiersService> _logger;
    private readonly ISageConnectionFactory _connectionFactory;

    public TiersService(
        ILogger<TiersService> logger,
        ISageConnectionFactory connectionFactory)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
    }

    public Task<TiersListResult> GetTiersAsync(
        string? type, bool actifsOnly, int skip, int take)
    {
        return StaRunner.RunAsync(() =>
        {
            using var connection = _connectionFactory.CreateConnection();
            try
            {
                connection.Open();
                if (!connection.IsOpen)
                    throw new Exception("Impossible d'ouvrir la base Sage.");

                // Accès dynamique aux objets COM
                dynamic baseCpta = connection.Instance;
                dynamic factory = baseCpta.FactoryTiers;
                dynamic coll;

                if (actifsOnly)
                {
                    coll = factory.QueryActifOrderNumero();
                }
                else if (!string.IsNullOrWhiteSpace(type) && 
                         !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
                {
                    int tiersType = type.Trim().ToLowerInvariant() switch
                    {
                        "client" => 0,          // TiersTypeClient
                        "fournisseur" => 1,     // TiersTypeFournisseur
                        "salarie" or "salarié" => 2, // TiersTypeSalarie
                        "autre" => 3,           // TiersTypeAutre
                        _ => 0
                    };
                    coll = factory.QueryTypeNumeroOrderNumero(tiersType, "", "ZZZZZZZZZZZZ");
                }
                else
                {
                    coll = factory.ListOrderNumero;
                }

                int total = coll.Count;
                if (take <= 0) take = 50;
                if (take > 200) take = 200;
                if (skip < 0) skip = 0;

                int start = Math.Min(skip + 1, total); // 1-based
                int end = Math.Min(skip + take, total);

                var list = new List<TiersDto>(Math.Max(0, end - start + 1));
                for (int i = start; i <= end; i++)
                {
                    dynamic tiers = coll.Item(i);
                    list.Add(new TiersDto(
                        tiers.CT_Num?.ToString() ?? "",
                        tiers.CT_Intitule?.ToString()
                    ));
                }

                return new TiersListResult(list, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des tiers");
                throw;
            }
        });
    }

    public Task<TiersDto?> GetTiersByNumeroAsync(string numero)
    {
        return StaRunner.RunAsync(() =>
        {
            using var connection = _connectionFactory.CreateConnection();
            try
            {
                connection.Open();
                if (!connection.IsOpen)
                    throw new Exception("Impossible d'ouvrir la base Sage.");

                dynamic baseCpta = connection.Instance;
                dynamic factory = baseCpta.FactoryTiers;

                if (!factory.ExistNumero(numero))
                    return null;

                dynamic tiers = factory.ReadNumero(numero);
                return new TiersDto(
                    tiers.CT_Num?.ToString() ?? "",
                    tiers.CT_Intitule?.ToString()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération du tiers {Numero}", numero);
                throw;
            }
        });
    }
}
