namespace ExConnector.Core.Interfaces;

/// <summary>
/// Interface pour la gestion des tokens d'administration
/// </summary>
public interface IAdminTokenService
{
    /// <summary>
    /// Crée un nouveau token d'authentification
    /// </summary>
    string Create(string user, TimeSpan ttl);
    
    /// <summary>
    /// Valide un token et retourne le nom d'utilisateur
    /// </summary>
    bool Validate(string token, out string? user);
}

