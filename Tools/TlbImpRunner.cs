using System.Diagnostics;
using System.Reflection;

namespace ExConnector.Tools;

public static class TlbImpRunner
{
    public class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? OutputPath { get; set; }
        public string? TlbImpPath { get; set; }
        public string? ComDllPath { get; set; }
    }

    /// <summary>
    /// Trouve TlbImp.exe dans les emplacements possibles
    /// </summary>
    public static string? FindTlbImp()
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

    /// <summary>
    /// Trouve Objets100c.dll dans les emplacements possibles
    /// </summary>
    public static string? FindComDll()
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

    /// <summary>
    /// Génère Objets100cLib.dll depuis Objets100c.dll
    /// </summary>
    public static Result Run(string? outputDir = null)
    {
        var result = new Result();

        try
        {
            // 1. Trouver TlbImp.exe
            var tlbImpPath = FindTlbImp();
            if (tlbImpPath == null)
            {
                result.Success = false;
                result.Message = "TlbImp.exe introuvable. Veuillez installer le SDK Windows avec les outils .NET Framework.";
                return result;
            }
            result.TlbImpPath = tlbImpPath;

            // 2. Trouver Objets100c.dll
            var comDllPath = FindComDll();
            if (comDllPath == null)
            {
                result.Success = false;
                result.Message = "Objets100c.dll introuvable. Veuillez installer Sage Objets Métiers.";
                return result;
            }
            result.ComDllPath = comDllPath;

            // 3. Déterminer le dossier de sortie
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = AppDomain.CurrentDomain.BaseDirectory;
            }

            var outputPath = Path.Combine(outputDir, "Objets100cLib.dll");
            result.OutputPath = outputPath;

            // 4. Vérifier les permissions d'écriture
            try
            {
                var testFile = Path.Combine(outputDir, $"test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                result.Success = false;
                result.Message = "Permissions insuffisantes. Veuillez exécuter en tant qu'administrateur.";
                return result;
            }

            // 5. Vérifier si le fichier existe et est verrouillé
            if (File.Exists(outputPath))
            {
                try
                {
                    // Tenter d'ouvrir le fichier en mode exclusif
                    using (var fs = new FileStream(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // Si on arrive ici, le fichier n'est pas verrouillé
                    }
                }
                catch (IOException)
                {
                    result.Success = false;
                    result.Message = "Le fichier Objets100cLib.dll est en cours d'utilisation. Veuillez arrêter ExConnector avant de régénérer l'interop.";
                    return result;
                }
            }

            // 6. Exécuter TlbImp
            var processInfo = new ProcessStartInfo
            {
                FileName = tlbImpPath,
                Arguments = $"\"{comDllPath}\" /out:\"{outputPath}\" /namespace:Sage100 /silent",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDir
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                result.Success = false;
                result.Message = "Impossible de démarrer TlbImp.exe";
                return result;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                result.Success = false;
                result.Message = $"Erreur TlbImp (code {process.ExitCode}):\n{error}\n{output}";
                return result;
            }

            // 7. Vérifier que le fichier a été généré
            if (!File.Exists(outputPath))
            {
                result.Success = false;
                result.Message = "Le fichier Objets100cLib.dll n'a pas été généré.";
                return result;
            }

            result.Success = true;
            result.Message = $"Interop généré avec succès : {outputPath}";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Erreur inattendue : {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Obtient la version du fichier Objets100c.dll
    /// </summary>
    public static string? GetComDllVersion()
    {
        var comDllPath = FindComDll();
        if (comDllPath == null || !File.Exists(comDllPath))
        {
            return null;
        }

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(comDllPath);
            return versionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Obtient la version de l'interop actuel
    /// </summary>
    public static string? GetInteropVersion()
    {
        try
        {
            var interopPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Objets100cLib.dll");
            if (!File.Exists(interopPath))
            {
                return null;
            }

            var assembly = Assembly.LoadFrom(interopPath);
            return assembly.GetName().Version?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

