using System.Text.Json;
using ExConnector.Core.Interfaces;
using ExConnector.Models;
using ExConnector.Infrastructure.Sage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics; // Required for Process

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints de gestion des dossiers Sage
/// </summary>
public static class SageFoldersEndpoints
{
    public static IEndpointRouteBuilder MapSageFoldersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/api/sage/folders", GetFoldersAsync);
        app.MapPost("/admin/api/sage/folders/save", SaveFolderAsync);
        app.MapPost("/admin/api/sage/folders/delete", DeleteFolderAsync);
        app.MapPost("/admin/api/sage/folders/activate", ActivateFolderAsync);
        // -------- Parser fichier .mae pour extraire infos SQL
        app.MapPost("/admin/api/sage/parse-mae", async (HttpContext ctx) =>
        {
            if (!CheckCsrf(ctx))
                return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

            var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(ctx.Request.Body) ?? new();
            var maePath = body.GetValueOrDefault("path");
            
            if (string.IsNullOrWhiteSpace(maePath))
                return Results.BadRequest(new { error = "Chemin .mae manquant" });

            if (!File.Exists(maePath))
                return Results.BadRequest(new { error = "Fichier .mae introuvable" });

            try
            {
                var lines = File.ReadAllLines(maePath);
                string? serveurSql = null;
                string? createur = null;
                string? type = null;

                // Parser le fichier .mae (format INI)
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("ServeurSQL=", StringComparison.OrdinalIgnoreCase))
                    {
                        serveurSql = trimmed.Substring("ServeurSQL=".Length).Trim();
                    }
                    else if (trimmed.StartsWith("Createur=", StringComparison.OrdinalIgnoreCase))
                    {
                        createur = trimmed.Substring("Createur=".Length).Trim();
                    }
                    else if (trimmed.StartsWith("Type=", StringComparison.OrdinalIgnoreCase))
                    {
                        type = trimmed.Substring("Type=".Length).Trim();
                    }
                }

                // Extraire le nom de la base depuis le nom du fichier
                var fileName = Path.GetFileNameWithoutExtension(maePath);

                return Results.Json(new
                {
                    ok = true,
                    companyServer = serveurSql ?? "",
                    companyDatabase = fileName ?? "",
                    createur = createur ?? "",
                    type = type ?? ""
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "Erreur lors de la lecture du fichier: " + ex.Message }, statusCode: 500);
            }
        });
        app.MapPost("/admin/api/sage/test-folder", TestFolderConnectionAsync);
        app.MapGet("/admin/api/sage/apps", DetectSageAppsAsync);

        return app;
    }

    private static IResult GetFoldersAsync(IConfiguration cfg, IDataProtectionService dataProtection)
    {
        var foldersConfig = cfg.GetSection("SageFolders").Get<SageFoldersConfig>() ?? new();
        
        // Déchiffrer les mots de passe pour l'affichage (masqués dans le frontend)
        foreach (var folder in foldersConfig.Folders)
        {
            if (folder.Mae?.Password != null && dataProtection.IsEncrypted(folder.Mae.Password))
            {
                try { folder.Mae.Password = dataProtection.Decrypt(folder.Mae.Password); }
                catch { folder.Mae.Password = null; }
            }
            if (folder.Gcm?.Password != null && dataProtection.IsEncrypted(folder.Gcm.Password))
            {
                try { folder.Gcm.Password = dataProtection.Decrypt(folder.Gcm.Password); }
                catch { folder.Gcm.Password = null; }
            }
            if (folder.Imo?.Password != null && dataProtection.IsEncrypted(folder.Imo.Password))
            {
                try { folder.Imo.Password = dataProtection.Decrypt(folder.Imo.Password); }
                catch { folder.Imo.Password = null; }
            }
            if (folder.Mdp?.Password != null && dataProtection.IsEncrypted(folder.Mdp.Password))
            {
                try { folder.Mdp.Password = dataProtection.Decrypt(folder.Mdp.Password); }
                catch { folder.Mdp.Password = null; }
            }
        }
        
        return Results.Json(foldersConfig.Folders);
    }

    private static async Task<IResult> SaveFolderAsync(
        HttpContext ctx,
        ISettingsRepository settingsRepo,
        IDataProtectionService dataProtection)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var folder = await JsonSerializer.DeserializeAsync<SageFolder>(ctx.Request.Body) ?? new();

        // Chiffrer les mots de passe avec DPAPI si nécessaire
        if (folder.Mae?.Password != null && !dataProtection.IsEncrypted(folder.Mae.Password))
        {
            folder.Mae.Password = dataProtection.Encrypt(folder.Mae.Password);
        }
        if (folder.Gcm?.Password != null && !dataProtection.IsEncrypted(folder.Gcm.Password))
        {
            folder.Gcm.Password = dataProtection.Encrypt(folder.Gcm.Password);
        }
        if (folder.Imo?.Password != null && !dataProtection.IsEncrypted(folder.Imo.Password))
        {
            folder.Imo.Password = dataProtection.Encrypt(folder.Imo.Password);
        }
        if (folder.Mdp?.Password != null && !dataProtection.IsEncrypted(folder.Mdp.Password))
        {
            folder.Mdp.Password = dataProtection.Encrypt(folder.Mdp.Password);
        }

        var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var foldersConfig = cfg.GetSection("SageFolders").Get<SageFoldersConfig>() ?? new();

        var existing = foldersConfig.Folders.FirstOrDefault(f => f.Id == folder.Id);
        if (existing != null)
        {
            var index = foldersConfig.Folders.IndexOf(existing);
            foldersConfig.Folders[index] = folder;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(folder.Id))
                folder.Id = Guid.NewGuid().ToString();
            foldersConfig.Folders.Add(folder);
        }

        if (folder.Active)
        {
            foreach (var f in foldersConfig.Folders.Where(f => f.Id != folder.Id))
                f.Active = false;
        }

        settingsRepo.SavePatch(new Dictionary<string, object?> { ["SageFolders"] = foldersConfig });
        return Results.Json(new { ok = true, folder });
    }

    private static async Task<IResult> DeleteFolderAsync(
        HttpContext ctx,
        ISettingsRepository settingsRepo)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(ctx.Request.Body) ?? new();
        var folderId = body.GetValueOrDefault("id");

        if (string.IsNullOrWhiteSpace(folderId))
            return Results.Json(new { ok = false, error = "ID manquant" }, statusCode: 400);

        var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var foldersConfig = cfg.GetSection("SageFolders").Get<SageFoldersConfig>() ?? new();

        foldersConfig.Folders.RemoveAll(f => f.Id == folderId);

        settingsRepo.SavePatch(new Dictionary<string, object?> { ["SageFolders"] = foldersConfig });
        return Results.Json(new { ok = true });
    }

    private static async Task<IResult> ActivateFolderAsync(
        HttpContext ctx,
        ISettingsRepository settingsRepo)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(ctx.Request.Body) ?? new();
        var folderId = body.GetValueOrDefault("id");

        if (string.IsNullOrWhiteSpace(folderId))
            return Results.Json(new { ok = false, error = "ID manquant" }, statusCode: 400);

        var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var foldersConfig = cfg.GetSection("SageFolders").Get<SageFoldersConfig>() ?? new();

        foreach (var f in foldersConfig.Folders)
            f.Active = false;

        var target = foldersConfig.Folders.FirstOrDefault(f => f.Id == folderId);
        if (target != null)
            target.Active = true;

        settingsRepo.SavePatch(new Dictionary<string, object?> { ["SageFolders"] = foldersConfig });
        return Results.Json(new { ok = true });
    }


    private static async Task<IResult> TestFolderConnectionAsync(
        HttpContext ctx,
        ISageConnectionFactory connectionFactory)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(ctx.Request.Body) ?? new();
        var maePath = body.GetValueOrDefault("maePath");
        var userName = body.GetValueOrDefault("userName");
        var password = body.GetValueOrDefault("password");
        var companyServer = body.GetValueOrDefault("companyServer");
        var companyDatabase = body.GetValueOrDefault("companyDatabase");

        if (string.IsNullOrWhiteSpace(maePath))
            return Results.BadRequest(new { success = false, message = "Chemin .mae requis" });

        try
        {
            var testConfig = new SageConfig
            {
                MAEPath = maePath,
                UserName = userName,
                Password = password,
                CompanyServer = companyServer,
                CompanyDatabaseName = companyDatabase
            };

            // Utiliser le nouveau système hybride avec AutoInteropGenerator
            var connection = connectionFactory.CreateConnection(testConfig);
            
            Console.WriteLine("[TEST CONNEXION] Utilisation du système hybride avec interop automatique...");
            Console.WriteLine("[TEST CONNEXION] Configuration : MAE={0}, Server={1}, Database={2}", 
                testConfig.MAEPath, testConfig.CompanyServer, testConfig.CompanyDatabaseName);

            connection.Open();
            
            if (connection.IsOpen)
            {
                Console.WriteLine("[TEST CONNEXION] ✅ Connexion réussie avec le système hybride");
                
                // Fermer la connexion dans le même thread STA
                StaRunner.RunAsync(() =>
                {
                    connection.Close();
                    Console.WriteLine("[TEST CONNEXION] ✅ Connexion fermée proprement");
                }).Wait();
                
                return Results.Json(new { 
                    success = true, 
                    message = "Connexion établie avec succès via le système hybride (interop automatique)" 
                });
            }
            else
            {
                Console.WriteLine("[TEST CONNEXION] ❌ Connexion échouée - IsOpen=false");
                return Results.Json(new { 
                    success = false, 
                    message = "La connexion n'a pas pu être établie (IsOpen=false)" 
                });
            }
        }
        catch (Exception ex)
        {
            return Results.Json(new { success = false, message = "Erreur : " + ex.Message });
        }
    }

    // Ancienne logique supprimée - maintenant utilise HybridSageConnection via connectionFactory

    private static object TestConnectionWithProgId(string progId, SageConfig config)
    {
        try
        {
            // Création dynamique de l'instance COM
            var type = Type.GetTypeFromProgID(progId);
            if (type == null)
            {
                return new { success = false, message = $"Impossible de créer le type COM pour {progId}" };
            }

            dynamic baseCpta = Activator.CreateInstance(type);
            if (baseCpta == null)
            {
                return new { success = false, message = "Impossible de créer l'instance COM Sage" };
            }

            return TestSageConnection(baseCpta, config, $"ProgID: {progId}");
        }
        catch (Exception ex)
        {
            return new { success = false, message = $"Erreur avec ProgID {progId}: " + ex.Message };
        }
    }

    private static object TestConnectionWithDirectDll(SageConfig config)
    {
        // Chemins possibles pour la DLL Sage
        var dllPaths = new[]
        {
            @"C:\Program Files (x86)\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Program Files\Common Files\Sage\Objets métiers\objets100c.dll",
            @"C:\Sage\Objets métiers\objets100c.dll"
        };

        Console.WriteLine("[CONNEXION] Recherche des DLL Sage...");
        foreach (var dllPath in dllPaths)
        {
            Console.WriteLine("[CONNEXION] Test du chemin: " + dllPath);
            if (File.Exists(dllPath))
            {
                Console.WriteLine("[CONNEXION] ✅ DLL trouvée: " + dllPath);
                try
                {
                    // Vérifier les permissions d'accès
                    var fileInfo = new FileInfo(dllPath);
                    Console.WriteLine("[CONNEXION] Taille DLL: " + fileInfo.Length + " bytes");
                    Console.WriteLine("[CONNEXION] Dernière modification: " + fileInfo.LastWriteTime);
                    
                    // La DLL est une DLL COM native, pas une assembly .NET
                    Console.WriteLine("[CONNEXION] DLL COM native détectée - tentative d'enregistrement...");
                    
                    // Essayer de forcer l'enregistrement de la DLL COM
                    try
                    {
                        // Utiliser regsvr32 pour enregistrer la DLL COM
                        var regsvr32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "regsvr32.exe");
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = regsvr32Path,
                            Arguments = $"/s \"{dllPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        
                        Console.WriteLine("[CONNEXION] Enregistrement de la DLL COM...");
                        using var process = Process.Start(startInfo);
                        process?.WaitForExit(5000); // Attendre max 5 secondes
                        Console.WriteLine("[CONNEXION] Enregistrement terminé avec code: " + (process?.ExitCode ?? -1));
                        
                        // Maintenant essayer de trouver le ProgID
                        Console.WriteLine("[CONNEXION] Recherche du ProgID après enregistrement...");
                        var progId = DetectAvailableSageProgId();
                        
                        if (!string.IsNullOrEmpty(progId))
                        {
                            Console.WriteLine("[CONNEXION] ✅ ProgID trouvé après enregistrement: " + progId);
                            return TestConnectionWithProgId(progId, config);
                        }
                        else
                        {
                            Console.WriteLine("[CONNEXION] ❌ Aucun ProgID trouvé même après enregistrement");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[CONNEXION] ❌ Erreur lors de l'enregistrement: " + ex.Message);
                    }
                    
                    // Fallback: essayer directement avec COM sans ProgID
                    Console.WriteLine("[CONNEXION] Tentative de création directe COM...");
                    try
                    {
                        // Essayer de créer directement l'objet COM
                        var comType = Type.GetTypeFromProgID("Objets100c.BSCPTAApplication100c");
                        if (comType != null)
                        {
                            var baseCpta = Activator.CreateInstance(comType);
                            Console.WriteLine("[CONNEXION] ✅ Instance COM créée directement");
                            return TestSageConnection(baseCpta, config, $"DLL COM: {Path.GetFileName(dllPath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[CONNEXION] ❌ Erreur création directe COM: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[CONNEXION] ❌ Erreur avec la DLL " + dllPath + ": " + ex.Message);
                    Console.WriteLine("[CONNEXION] Type d'erreur: " + ex.GetType().Name);
                    Console.WriteLine("[CONNEXION] Stack trace: " + ex.StackTrace);
                }
            }
            else
            {
                Console.WriteLine("[CONNEXION] ❌ DLL non trouvée: " + dllPath);
            }
        }

        return new { success = false, message = "Aucune DLL Sage trouvée ou impossible de la charger. Vérifiez l'installation de Sage 100 Objets Métiers." };
    }

    private static object TestSageConnection(dynamic baseCpta, SageConfig config, string method)
    {
        try
        {
            // Configuration de la connexion
            if (!string.IsNullOrWhiteSpace(config.MAEPath))
            {
                baseCpta.Name = config.MAEPath;
            }
            else if (!string.IsNullOrWhiteSpace(config.CompanyServer) && !string.IsNullOrWhiteSpace(config.CompanyDatabaseName))
            {
                baseCpta.CompanyServer = config.CompanyServer;
                baseCpta.CompanyDatabaseName = config.CompanyDatabaseName;
            }
            else
            {
                return new { success = false, message = "Configuration incomplète : MAEPath ou CompanyServer + CompanyDatabaseName requis." };
            }

            // Configuration des identifiants
            baseCpta.Loggable.UserName = config.UserName ?? string.Empty;
            baseCpta.Loggable.UserPwd = config.Password ?? string.Empty;

            // Ouverture de la connexion
            baseCpta.Open();
            
            if (baseCpta.IsOpen)
            {
                return new { success = true, message = $"Connexion réussie à la base Sage ! ({method})" };
            }
            else
            {
                return new { success = false, message = "Échec : Open() n'a pas retourné IsOpen=true." };
            }
        }
        finally
        {
            try 
            { 
                if (baseCpta.IsOpen) 
                    baseCpta.Close(); 
            } 
            catch { }
        }
    }

    private static string DetectAvailableSageProgId()
    {
        var possibleProgIds = new[]
        {
            // ProgIDs standard Sage 100
            "Objets100c.BSCPTAApplication100c",  // Version standard
            "Objets100c.BSCPTAApplication100c.1", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.2", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.3", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.4", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.5", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.6", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.7", // Version avec numéro
            "Objets100c.BSCPTAApplication100c.8", // Version avec numéro
            
            // ProgIDs alternatifs possibles
            "BSCPTAApplication100c",
            "Sage.BSCPTAApplication100c",
            "Sage100c.BSCPTAApplication100c",
            "Objets100c.Application",
            "Objets100c.Application.1",
            "Objets100c.Application.2",
            "Objets100c.Application.3",
            
            // ProgIDs pour différentes versions
            "Objets100c.BSCPTAApplication",
            "Objets100c.BSCPTAApplication.1",
            "Objets100c.BSCPTAApplication.2",
            "Objets100c.BSCPTAApplication.3",
            
            // ProgIDs avec versions spécifiques
            "Objets100c.BSCPTAApplication100c.12.0",
            "Objets100c.BSCPTAApplication100c.11.0",
            "Objets100c.BSCPTAApplication100c.10.0",
        };

        Console.WriteLine("[DÉTECTION] Test de " + possibleProgIds.Length + " ProgIDs Sage...");

        foreach (var progId in possibleProgIds)
        {
            try
            {
                Console.WriteLine("[DÉTECTION] Test du ProgID: " + progId);
                var type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    Console.WriteLine("[DÉTECTION] ✅ ProgID trouvé et valide: " + progId);
                    return progId;
                }
                else
                {
                    Console.WriteLine("[DÉTECTION] ❌ ProgID non trouvé: " + progId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DÉTECTION] ❌ Erreur avec " + progId + ": " + ex.Message);
            }
        }

        Console.WriteLine("[DÉTECTION] ⚠️ Aucun ProgID Sage trouvé");
        return string.Empty;
    }

    private static IResult DetectSageAppsAsync()
    {
        string[] baseDirs = new[]
        {
            @"C:\Program Files (x86)\Sage",
            @"C:\Program Files\Sage"
        };

        var compta = DetectApp("Maestria.exe", baseDirs);
        var gescom = DetectApp("GecoMaes.exe", baseDirs);

        return Results.Json(new
        {
            comptabilite = compta,
            gestionCommerciale = gescom
        });
    }

    private static object DetectApp(string exeName, string[] baseDirs)
    {
        string? found = null;
        foreach (var baseDir in baseDirs)
        {
            try
            {
                if (!Directory.Exists(baseDir)) continue;
                var files = Directory.EnumerateFiles(baseDir, exeName, SearchOption.AllDirectories).Take(1);
                found = files.FirstOrDefault();
                if (found is not null) break;
            }
            catch { }
        }

        if (found is null)
            return new { installed = false, path = (string?)null, fileVersion = (string?)null };

        try
        {
            var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(found);
            return new
            {
                installed = true,
                path = found,
                fileVersion = vi.FileVersion,
                productVersion = vi.ProductVersion,
                productName = vi.ProductName
            };
        }
        catch
        {
            return new { installed = true, path = found, fileVersion = (string?)null };
        }
    }

    private static bool CheckCsrf(HttpContext ctx)
    {
        var cookie = ctx.Request.Cookies["EXADM-CSRF"];
        var header = ctx.Request.Headers.TryGetValue("X-CSRF", out var hv) ? hv.ToString() : null;
        return !string.IsNullOrWhiteSpace(cookie) && string.Equals(header, cookie, StringComparison.Ordinal);
    }
}

