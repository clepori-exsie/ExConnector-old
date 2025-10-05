using ExConnector.Core.DTOs;
using ExConnector.Models;

namespace ExConnector.Core.Interfaces;

/// <summary>
/// Interface pour les opérations Sage via Objets Métiers
/// </summary>
public interface ISageService
{
    /// <summary>
    /// Teste la connexion à la base Sage avec la config actuelle
    /// </summary>
    Task<ServiceResult> PingAsync();
    
    /// <summary>
    /// Teste la connexion avec une configuration spécifique
    /// </summary>
    Task<ServiceResult> TestConnectionAsync(SageConfig config);
}

