namespace ExConnector.Core.Models;

/// <summary>
/// Informations sur la version de Sage 100 Objets Métiers installée
/// </summary>
public class SageVersionInfo
{
    /// <summary>
    /// Chemin complet vers objets100c.dll
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Version du fichier (ex: 12.0.0.0)
    /// </summary>
    public string? FileVersion { get; set; }

    /// <summary>
    /// Version produit (ex: 12.00)
    /// </summary>
    public string? ProductVersion { get; set; }

    /// <summary>
    /// Nom du produit
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Indique si la DLL est trouvée et accessible
    /// </summary>
    public bool IsAvailable => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

    /// <summary>
    /// Représentation textuelle
    /// </summary>
    public override string ToString()
    {
        return IsAvailable 
            ? $"{ProductName} v{FileVersion}" 
            : "Non détecté";
    }
}

