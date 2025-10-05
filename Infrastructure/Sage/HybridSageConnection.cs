using ExConnector.Core.Interfaces;
using ExConnector.Core.Models;
using ExConnector.Models;
using Microsoft.Extensions.Logging;

namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Connexion Sage hybride - Combine interop automatique + fallback dynamique
/// Solution autonome compatible toutes versions Sage
/// </summary>
public class HybridSageConnection : IDynamicSageConnection
{
    private readonly ILogger<HybridSageConnection> _logger;
    private readonly SageConfig _config;
    private readonly IDataProtectionService _dataProtection;
    private readonly AutoInteropGenerator _interopGenerator;
    private dynamic? _baseCpta;
    private bool _disposed;

    public HybridSageConnection(
        ILogger<HybridSageConnection> logger,
        SageConfig config,
        IDataProtectionService dataProtection,
        AutoInteropGenerator interopGenerator)
    {
        _logger = logger;
        _config = config;
        _dataProtection = dataProtection;
        _interopGenerator = interopGenerator;
    }

    public bool IsOpen => _baseCpta?.IsOpen == true;

    public dynamic Instance => _baseCpta ?? throw new InvalidOperationException("Connexion non ouverte");

    public void Open()
    {
        try
        {
            _logger.LogInformation("Ouverture de la connexion Sage avec approche hybride...");

            // Utiliser StaRunner pour les appels COM (comme dans le backup original)
            var result = StaRunner.RunAsync(() =>
            {
                // 1. Essayer l'approche interop automatique (recommandée)
                if (TryOpenWithAutoInterop())
                {
                    _logger.LogInformation("✅ Connexion Sage ouverte avec interop automatique");
                    return true;
                }

                // 2. Fallback vers l'approche dynamique COM
                _logger.LogWarning("Interop automatique échoué, tentative avec approche dynamique COM...");
                if (TryOpenWithDynamicCom())
                {
                    _logger.LogInformation("✅ Connexion Sage ouverte avec approche dynamique COM");
                    return true;
                }

                return false;
            }).Result;

            if (!result)
            {
                throw new InvalidOperationException("Impossible d'ouvrir la connexion Sage avec aucune des approches disponibles");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'ouverture de la connexion Sage");
            throw;
        }
    }

    private bool TryOpenWithAutoInterop()
    {
        try
        {
            _logger.LogInformation("🔄 Tentative d'ouverture avec interop automatique...");

            // 1. Générer ou récupérer l'interop
            _logger.LogInformation("📦 Génération/récupération de l'interop...");
            var interopResult = _interopGenerator.GenerateOrGetInterop();
            if (!interopResult.Success)
            {
                _logger.LogWarning("❌ Génération d'interop échouée : {Message}", interopResult.Message);
                return false;
            }

            _logger.LogInformation("✅ Interop disponible : {InteropPath}", interopResult.InteropPath);

            // 2. Charger l'assembly interop
            _logger.LogInformation("📚 Chargement de l'assembly interop...");
            var assembly = _interopGenerator.LoadInteropAssembly(interopResult.InteropPath!);
            if (assembly == null)
            {
                _logger.LogWarning("❌ Impossible de charger l'assembly interop");
                return false;
            }

            _logger.LogInformation("✅ Assembly interop chargé avec succès");

            // 3. Créer l'instance Sage
            _logger.LogInformation("🏗️ Création de l'instance Sage...");
            _baseCpta = _interopGenerator.CreateSageInstance(assembly);
            if (_baseCpta == null)
            {
                _logger.LogWarning("❌ Impossible de créer l'instance Sage depuis l'interop");
                return false;
            }

            _logger.LogInformation("✅ Instance Sage créée avec succès");

            // 4. Configurer et ouvrir la connexion
            _logger.LogInformation("⚙️ Configuration de la connexion...");
            ConfigureConnection();
            
            _logger.LogInformation("🔓 Ouverture de la connexion Sage...");
            _baseCpta.Open();

            if (_baseCpta.IsOpen)
            {
                _logger.LogInformation("✅ Connexion Sage ouverte avec succès via interop automatique");
                return true;
            }

            _logger.LogWarning("❌ Open() n'a pas reporté IsOpen=true");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Échec de l'approche interop automatique");
            return false;
        }
    }

    private bool TryOpenWithDynamicCom()
    {
        try
        {
            _logger.LogDebug("Tentative d'ouverture avec approche dynamique COM...");

            // Utiliser l'approche dynamique COM existante
            var progId = DetectSageProgId();
            var type = Type.GetTypeFromProgID(progId);

            if (type == null)
            {
                _logger.LogWarning("ProgID '{ProgId}' non trouvé", progId);
                return false;
            }

            _baseCpta = Activator.CreateInstance(type);
            if (_baseCpta == null)
            {
                _logger.LogWarning("Impossible de créer l'instance COM Sage");
                return false;
            }

            // Configurer et ouvrir la connexion
            ConfigureConnection();
            _baseCpta.Open();

            if (_baseCpta.IsOpen)
            {
                _logger.LogInformation("Connexion Sage ouverte avec succès via COM dynamique");
                return true;
            }

            _logger.LogWarning("Open() n'a pas reporté IsOpen=true");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec de l'approche dynamique COM");
            return false;
        }
    }

    private string DetectSageProgId()
    {
        var possibleProgIds = new[]
        {
            "Objets100c.BSCPTAApplication100c",
            "Objets100c.BSCPTAApplication100c.1",
            "Objets100c.BSCPTAApplication100c.2",
            "Objets100c.BSCPTAApplication100c.3",
            "Objets100c.BSCPTAApplication100c.4",
            "Objets100c.BSCPTAApplication100c.5",
            "Objets100c.BSCPTAApplication100c.6",
            "Objets100c.BSCPTAApplication100c.7",
            "Objets100c.BSCPTAApplication100c.8"
        };

        foreach (var progId in possibleProgIds)
        {
            try
            {
                var type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    _logger.LogDebug("ProgID Sage détecté : {ProgId}", progId);
                    return progId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ProgID {ProgId} non disponible", progId);
            }
        }

        _logger.LogWarning("Aucun ProgID Sage détecté, utilisation du ProgID standard");
        return "Objets100c.BSCPTAApplication100c";
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
        var password = _config.Password;
        if (!string.IsNullOrEmpty(password) && password.StartsWith("ENCRYPTED:"))
        {
            password = _dataProtection.Decrypt(password.Substring(10));
        }
        _baseCpta.Loggable.UserPwd = password ?? string.Empty;

        _logger.LogDebug("Configuration Sage terminée");
    }

    public void Close()
    {
        try
        {
            if (_baseCpta?.IsOpen == true)
            {
                _baseCpta.Close();
                _logger.LogDebug("Connexion Sage fermée");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la fermeture de la connexion Sage");
        }
        finally
        {
            _baseCpta = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}
