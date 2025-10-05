using System.Diagnostics;
using ExConnector.Core.Models;
using Microsoft.Extensions.Logging;

namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Service de détection de la version de Sage 100 Objets Métiers installée
/// </summary>
public class SageVersionDetector
{
    private readonly ILogger<SageVersionDetector> _logger;

    public SageVersionDetector(ILogger<SageVersionDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Détecte la version de Sage 100 Objets Métiers installée sur le système
    /// </summary>
    public SageVersionInfo DetectVersion()
    {
        var searchPaths = new[]
        {
            @"C:\Program Files (x86)\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Program Files\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Sage\Objets métiers\objets100c.dll"
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(path);
                    
                    var info = new SageVersionInfo
                    {
                        FilePath = path,
                        FileVersion = versionInfo.FileVersion,
                        ProductVersion = versionInfo.ProductVersion,
                        ProductName = versionInfo.ProductName
                    };

                    _logger.LogInformation(
                        "Sage 100 Objets Métiers détecté : {ProductName} v{Version} ({Path})",
                        info.ProductName,
                        info.FileVersion,
                        path
                    );

                    return info;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de la lecture de {Path}", path);
                }
            }
        }

        _logger.LogWarning("Aucune version de Sage 100 Objets Métiers détectée");
        
        return new SageVersionInfo
        {
            FilePath = string.Empty,
            FileVersion = "Non détecté",
            ProductVersion = "Non détecté",
            ProductName = "Sage 100 Objets Métiers"
        };
    }

    /// <summary>
    /// Vérifie si Sage 100 Objets Métiers est installé
    /// </summary>
    public bool IsSageInstalled()
    {
        var version = DetectVersion();
        return version.IsAvailable;
    }
}

