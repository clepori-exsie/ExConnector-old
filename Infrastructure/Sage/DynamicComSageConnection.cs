using System.Runtime.InteropServices;
using ExConnector.Core.Interfaces;
using ExConnector.Core.Models;
using ExConnector.Models;
using Microsoft.Extensions.Logging;

namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Connexion COM dynamique à Sage 100 - Compatible toutes versions
/// </summary>
public class DynamicComSageConnection : IDynamicSageConnection
{
    private readonly ILogger<DynamicComSageConnection> _logger;
    private readonly SageConfig _config;
    private readonly IDataProtectionService _dataProtection;
    private dynamic? _baseCpta;
    private bool _disposed;

    public DynamicComSageConnection(
        ILogger<DynamicComSageConnection> logger,
        SageConfig config,
        IDataProtectionService dataProtection)
    {
        _logger = logger;
        _config = config;
        _dataProtection = dataProtection;
    }

    public bool IsOpen => _baseCpta?.IsOpen == true;

    public dynamic Instance => _baseCpta ?? throw new InvalidOperationException("Connexion non ouverte");

    public void Open()
    {
        try
        {
            // Détection automatique du ProgID - compatible toutes versions Sage
            var progId = DetectSageProgId();
            var type = Type.GetTypeFromProgID(progId);

            if (type == null)
            {
                throw new COMException(
                    $"Impossible de trouver le ProgID '{progId}'. " +
                    "Sage 100 Objets Métiers n'est peut-être pas installé ou enregistré."
                );
            }

            _baseCpta = Activator.CreateInstance(type) 
                ?? throw new InvalidOperationException("Impossible de créer l'instance COM Sage");
            _logger.LogDebug("Instance COM Sage créée avec succès");

            // Configuration de la connexion
            ConfigureConnection();

            // Ouverture
            _baseCpta.Open();
            _logger.LogInformation("Connexion Sage ouverte avec succès");
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Erreur COM lors de l'ouverture de la connexion Sage");
            throw new InvalidOperationException($"Erreur COM Sage : {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'ouverture de la connexion Sage");
            throw;
        }
    }

    private void ConfigureConnection()
    {
        if (_baseCpta == null)
            throw new InvalidOperationException("L'instance COM n'est pas initialisée");

        // Configuration MAE ou SQL
        if (!string.IsNullOrWhiteSpace(_config.MAEPath))
        {
            _baseCpta.Name = _config.MAEPath;
            _logger.LogDebug("Configuration MAE : {MAEPath}", _config.MAEPath);
        }
        else if (!string.IsNullOrWhiteSpace(_config.CompanyServer) &&
                 !string.IsNullOrWhiteSpace(_config.CompanyDatabaseName))
        {
            _baseCpta.CompanyServer = _config.CompanyServer;
            _baseCpta.CompanyDatabaseName = _config.CompanyDatabaseName;
            _logger.LogDebug(
                "Configuration SQL : Server={Server}, Database={Database}",
                _config.CompanyServer,
                _config.CompanyDatabaseName
            );
        }
        else
        {
            throw new InvalidOperationException(
                "Configuration incomplète : MAEPath ou (CompanyServer + CompanyDatabaseName) requis"
            );
        }

        // Credentials
        _baseCpta.Loggable.UserName = _config.UserName ?? string.Empty;

        // Déchiffrement du mot de passe si nécessaire
        var password = _config.Password ?? string.Empty;
        if (_dataProtection.IsEncrypted(password))
        {
            try
            {
                password = _dataProtection.Decrypt(password);
                _logger.LogDebug("Mot de passe Sage déchiffré");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec du déchiffrement du mot de passe Sage");
                throw new InvalidOperationException("Impossible de déchiffrer le mot de passe Sage", ex);
            }
        }

        _baseCpta.Loggable.UserPwd = password;
    }

    public void Close()
    {
        if (_baseCpta == null) return;
        
        if (IsOpen)
        {
            try
            {
                _baseCpta.Close();
                _logger.LogDebug("Connexion Sage fermée");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors de la fermeture de la connexion Sage");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Close();

        if (_baseCpta != null)
        {
            try
            {
                Marshal.ReleaseComObject(_baseCpta);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors de la libération de l'objet COM");
            }
            _baseCpta = null;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Détecte automatiquement le ProgID de Sage 100 Objets Métiers
    /// </summary>
    private string DetectSageProgId()
    {
        // Liste des ProgIDs possibles selon les versions Sage
        var possibleProgIds = new[]
        {
            "Objets100c.BSCPTAApplication100c",  // Version standard
            "Objets100c.BSCPTAApplication100c.1", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.2", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.3", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.4", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.5", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.6", // Version avec numéro
        };

        _logger.LogInformation("Début de la détection des ProgIDs Sage disponibles...");

        foreach (var progId in possibleProgIds)
        {
            try
            {
                var type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    _logger.LogInformation("✅ ProgID Sage détecté et disponible : {ProgId}", progId);
                    return progId;
                }
                else
                {
                    _logger.LogDebug("❌ ProgID {ProgId} non disponible (Type.GetTypeFromProgID retourne null)", progId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "❌ ProgID {ProgId} non disponible (exception)", progId);
            }
        }

        // Fallback sur le ProgID standard
        _logger.LogWarning("⚠️ Aucun ProgID Sage détecté, utilisation du ProgID standard");
        return "Objets100c.BSCPTAApplication100c";
    }
}

