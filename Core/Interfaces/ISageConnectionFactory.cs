using ExConnector.Models;

namespace ExConnector.Core.Interfaces;

/// <summary>
/// Factory pour créer des connexions Sage dynamiques
/// </summary>
public interface ISageConnectionFactory
{
    /// <summary>
    /// Crée une connexion avec la configuration par défaut
    /// </summary>
    IDynamicSageConnection CreateConnection();
    
    /// <summary>
    /// Crée une connexion avec une configuration spécifique
    /// </summary>
    IDynamicSageConnection CreateConnection(SageConfig config);
}
