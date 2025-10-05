using ExConnector.Core.DTOs;

namespace ExConnector.Core.Interfaces;

/// <summary>
/// Interface pour la gestion des tiers Sage
/// </summary>
public interface ITiersService
{
    /// <summary>
    /// Récupère une liste de tiers avec pagination
    /// </summary>
    Task<TiersListResult> GetTiersAsync(string? type, bool actifsOnly, int skip, int take);
    
    /// <summary>
    /// Récupère un tiers par son numéro
    /// </summary>
    Task<TiersDto?> GetTiersByNumeroAsync(string numero);
}

