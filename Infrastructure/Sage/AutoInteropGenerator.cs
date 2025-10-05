using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Générateur automatique d'interopération COM pour Sage 100
/// Compatible toutes versions - Génération à la volée
/// </summary>
public class AutoInteropGenerator
{
    private readonly ILogger<AutoInteropGenerator> _logger;
    private static readonly object _lock = new object();
    private static string? _cachedInteropPath;

    public AutoInteropGenerator(ILogger<AutoInteropGenerator> logger)
    {
        _logger = logger;
    }

    public class GenerationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? InteropPath { get; set; }
        public string? ComDllPath { get; set; }
        public string? ComDllVersion { get; set; }
    }

    /// <summary>
    /// Génère ou récupère l'interopération COM pour Sage 100
    /// </summary>
    public GenerationResult GenerateOrGetInterop()
    {
        lock (_lock)
        {
            try
            {
                // 1. Vérifier si un interop est déjà en cache
                if (!string.IsNullOrEmpty(_cachedInteropPath) && File.Exists(_cachedInteropPath))
                {
                    _logger.LogDebug("Interop en cache trouvé : {InteropPath}", _cachedInteropPath);
                    return new GenerationResult
                    {
                        Success = true,
                        Message = "Interop récupéré depuis le cache",
                        InteropPath = _cachedInteropPath
                    };
                }

                // 2. Trouver la DLL COM Sage
                var comDllPath = FindSageComDll();
                if (comDllPath == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        Message = "Objets100c.dll introuvable. Veuillez installer Sage 100 Objets Métiers."
                    };
                }

                // 3. Obtenir la version de la DLL COM
                var comDllVersion = GetComDllVersion(comDllPath);
                _logger.LogInformation("DLL COM Sage trouvée : {ComDllPath} (v{Version})", comDllPath, comDllVersion);

                // 4. Déterminer le chemin de l'interop
                var outputDir = AppDomain.CurrentDomain.BaseDirectory;
                var interopPath = Path.Combine(outputDir, $"Objets100cLib_{comDllVersion?.Replace(".", "_") ?? "unknown"}.dll");

                // 5. Vérifier si l'interop existe déjà pour cette version
                if (File.Exists(interopPath))
                {
                    _logger.LogDebug("Interop existant trouvé pour la version {Version} : {InteropPath}", comDllVersion, interopPath);
                    _cachedInteropPath = interopPath;
                    return new GenerationResult
                    {
                        Success = true,
                        Message = $"Interop existant pour la version {comDllVersion}",
                        InteropPath = interopPath,
                        ComDllPath = comDllPath,
                        ComDllVersion = comDllVersion
                    };
                }

                // 6. Générer l'interop
                return GenerateInterop(comDllPath, interopPath, comDllVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération/récupération de l'interop");
                return new GenerationResult
                {
                    Success = false,
                    Message = $"Erreur inattendue : {ex.Message}"
                };
            }
        }
    }

    private GenerationResult GenerateInterop(string comDllPath, string interopPath, string? comDllVersion)
    {
        try
        {
            _logger.LogInformation("Génération de l'interop pour Sage {Version}...", comDllVersion);

            // 1. Trouver TlbImp.exe
            var tlbImpPath = FindTlbImp();
            if (tlbImpPath == null)
            {
                return new GenerationResult
                {
                    Success = false,
                    Message = "TlbImp.exe introuvable. Veuillez installer le SDK Windows."
                };
            }

            // 2. Vérifier les permissions
            var outputDir = Path.GetDirectoryName(interopPath)!;
            if (!CheckWritePermissions(outputDir))
            {
                return new GenerationResult
                {
                    Success = false,
                    Message = "Permissions insuffisantes. Veuillez exécuter en tant qu'administrateur."
                };
            }

            // 3. Exécuter TlbImp
            var processInfo = new ProcessStartInfo
            {
                FileName = tlbImpPath,
                Arguments = $"\"{comDllPath}\" /out:\"{interopPath}\" /namespace:Sage100 /silent",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDir
            };

            _logger.LogDebug("Exécution de TlbImp : {Arguments}", processInfo.Arguments);

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new GenerationResult
                {
                    Success = false,
                    Message = "Impossible de démarrer TlbImp.exe"
                };
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000); // Timeout 30 secondes

            if (process.ExitCode != 0)
            {
                _logger.LogError("TlbImp échec (code {ExitCode}) : {Error}", process.ExitCode, error);
                return new GenerationResult
                {
                    Success = false,
                    Message = $"Erreur TlbImp (code {process.ExitCode}) : {error}"
                };
            }

            // 4. Vérifier que le fichier a été généré
            if (!File.Exists(interopPath))
            {
                return new GenerationResult
                {
                    Success = false,
                    Message = "Le fichier interop n'a pas été généré."
                };
            }

            _logger.LogInformation("✅ Interop généré avec succès : {InteropPath}", interopPath);
            _cachedInteropPath = interopPath;

            return new GenerationResult
            {
                Success = true,
                Message = $"Interop généré avec succès pour la version {comDllVersion}",
                InteropPath = interopPath,
                ComDllPath = comDllPath,
                ComDllVersion = comDllVersion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la génération de l'interop");
            return new GenerationResult
            {
                Success = false,
                Message = $"Erreur inattendue : {ex.Message}"
            };
        }
    }

    private static string? FindSageComDll()
    {
        var possiblePaths = new[]
        {
            @"C:\Program Files (x86)\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Program Files\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Sage\Objets métiers\objets100c.dll"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? FindTlbImp()
    {
        var possiblePaths = new[]
        {
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.1 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\TlbImp.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools\TlbImp.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? GetComDllVersion(string dllPath)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
            return versionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }

    private static bool CheckWritePermissions(string directory)
    {
        try
        {
            var testFile = Path.Combine(directory, $"test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Charge l'assembly interop généré
    /// </summary>
    public Assembly? LoadInteropAssembly(string interopPath)
    {
        try
        {
            if (!File.Exists(interopPath))
            {
                _logger.LogError("Fichier interop introuvable : {InteropPath}", interopPath);
                return null;
            }

            _logger.LogDebug("Chargement de l'assembly interop : {InteropPath}", interopPath);
            var assembly = Assembly.LoadFrom(interopPath);
            _logger.LogDebug("Assembly interop chargé avec succès");
            return assembly;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement de l'assembly interop");
            return null;
        }
    }

    /// <summary>
    /// Crée une instance de BSCPTAApplication100c depuis l'assembly interop
    /// </summary>
    public dynamic? CreateSageInstance(Assembly interopAssembly)
    {
        try
        {
            var types = interopAssembly.GetTypes();
            _logger.LogDebug("Types disponibles dans l'assembly interop : {Types}", 
                string.Join(", ", types.Select(t => t.Name).Take(10)));

            // Chercher la classe concrète BSCPTAApplication100cClass
            var concreteClass = types.FirstOrDefault(t => 
                t.Name.Equals("BSCPTAApplication100cClass", StringComparison.OrdinalIgnoreCase) ||
                (t.Name.Contains("BSCPTAApplication") && t.Name.EndsWith("Class") && t.IsClass));

            if (concreteClass != null)
            {
                _logger.LogDebug("Classe concrète COM trouvée : {TypeName}", concreteClass.FullName);
                
                try
                {
                    var instance = Activator.CreateInstance(concreteClass);
                    _logger.LogDebug("Instance COM créée avec succès depuis la classe concrète");
                    return instance;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Impossible de créer l'instance depuis la classe concrète");
                }
            }

            // Fallback : essayer avec l'interface BSCPTAApplication100c
            var interfaceType = types.FirstOrDefault(t => 
                t.Name.Equals("BSCPTAApplication100c", StringComparison.OrdinalIgnoreCase) ||
                (t.Name.Contains("BSCPTAApplication") && !t.Name.StartsWith("I") && t.IsInterface));

            if (interfaceType != null)
            {
                _logger.LogDebug("Interface COM trouvée : {TypeName}", interfaceType.FullName);
                
                // Utiliser Type.GetTypeFromProgID pour créer l'objet COM réel
                var progId = "Objets100c.BSCPTAApplication100c";
                _logger.LogDebug("Création de l'objet COM via ProgID : {ProgId}", progId);
                
                var comType = Type.GetTypeFromProgID(progId);
                if (comType != null)
                {
                    _logger.LogDebug("Type COM trouvé via ProgID : {ComTypeName}", comType.FullName);
                    var comInstance = Activator.CreateInstance(comType);
                    _logger.LogDebug("Instance COM créée avec succès");
                    return comInstance;
                }
                
                _logger.LogWarning("ProgID {ProgId} non trouvé", progId);
            }

            _logger.LogError("Aucune interface BSCPTAApplication trouvée dans l'assembly interop");
            _logger.LogError("Types disponibles : {Types}", string.Join(", ", types.Select(t => t.Name)));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de l'instance Sage");
            return null;
        }
    }
}
