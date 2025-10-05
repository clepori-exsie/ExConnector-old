namespace ExConnector.Core.Interfaces;

/// <summary>
/// Interface pour la gestion des paramètres de configuration
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Chemin du fichier de configuration
    /// </summary>
    string FilePath { get; }
    
    /// <summary>
    /// Sauvegarde des modifications partielles de configuration
    /// </summary>
    void SavePatch(Dictionary<string, object?> patch);
    
    /// <summary>
    /// Initialise le chemin du fichier de configuration
    /// </summary>
    void Initialize(string filePath);
}

