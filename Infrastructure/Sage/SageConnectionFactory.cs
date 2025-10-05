using ExConnector.Core.Interfaces;
using ExConnector.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Factory pour créer des connexions Sage avec approche hybride - Compatible toutes versions
/// Gère automatiquement la sélection du dossier actif (multidossier)
/// </summary>
public class SageConnectionFactory : ISageConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionService _dataProtection;
    private readonly ILogger<HybridSageConnection> _logger;
    private readonly AutoInteropGenerator _interopGenerator;

    public SageConnectionFactory(
        IConfiguration configuration,
        IDataProtectionService dataProtection,
        ILogger<HybridSageConnection> logger,
        AutoInteropGenerator interopGenerator)
    {
        _configuration = configuration;
        _dataProtection = dataProtection;
        _logger = logger;
        _interopGenerator = interopGenerator;
    }

    public IDynamicSageConnection CreateConnection()
    {
        // 1. Chercher le dossier actif dans SageFolders (gestion multidossier)
        var foldersConfig = _configuration.GetSection("SageFolders").Get<SageFoldersConfig>();
        var activeFolder = foldersConfig?.Folders?.FirstOrDefault(f => f.Active);

        SageConfig config;

        if (activeFolder != null)
        {
            // Utiliser le dossier actif
            _logger.LogInformation("Utilisation du dossier actif : {FolderName} (ID: {FolderId})", 
                activeFolder.Name, activeFolder.Id);

            config = new SageConfig
            {
                MAEPath = activeFolder.Mae?.Path,
                UserName = activeFolder.Mae?.User,
                Password = activeFolder.Mae?.Password,
                CompanyServer = activeFolder.CompanyServer,
                CompanyDatabaseName = activeFolder.CompanyDatabase
            };
        }
        else
        {
            // Fallback sur SageConfig (ancienne configuration ou aucun dossier actif)
            _logger.LogInformation("Aucun dossier actif trouvé, utilisation de SageConfig");
            config = _configuration.GetSection("SageConfig").Get<SageConfig>() ?? new();
        }

        return CreateConnection(config);
    }

    public IDynamicSageConnection CreateConnection(SageConfig config)
    {
        return new HybridSageConnection(_logger, config, _dataProtection, _interopGenerator);
    }
}

