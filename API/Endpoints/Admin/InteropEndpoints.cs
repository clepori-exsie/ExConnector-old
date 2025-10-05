using System.Diagnostics;
using ExConnector.Infrastructure.Sage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints pour la gestion des interop (legacy - COM dynamique maintenant)
/// </summary>
public static class InteropEndpoints
{
    public static IEndpointRouteBuilder MapInteropEndpoints(this IEndpointRouteBuilder app)
    {
        // Endpoints OM/Interop
        app.MapGet("/admin/api/om-info", GetOmInfoAsync);
        app.MapGet("/admin/api/interop/info", GetInteropInfoAsync);
        app.MapPost("/admin/api/interop/regenerate", (HttpContext ctx) => RegenerateInteropAsync(ctx));

        return app;
    }

    private static IResult GetOmInfoAsync()
    {
        // COM Dynamique - détection Sage OM
        string? comPathGuess = null;
        var p1 = @"C:\Program Files (x86)\Common Files\Sage\Objets métiers\objets100c.dll";
        var p2 = @"C:\Program Files\Common Files\Sage\Objets métiers\objets100c.dll";
        if (File.Exists(p1)) comPathGuess = p1;
        else if (File.Exists(p2)) comPathGuess = p2;

        string? fileVer = null;
        string? comVer = null;

        if (comPathGuess is not null && File.Exists(comPathGuess))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(comPathGuess);
                fileVer = vi.FileVersion;
                comVer = vi.FileVersion;
            }
            catch { }
        }

        return Results.Json(new
        {
            interop = "COM Dynamique (Runtime)",
            interopFileVersion = "Auto-Detect",
            comDll = comPathGuess,
            comFileVersion = comVer
        });
    }

    private static IResult GetInteropInfoAsync()
    {
        // Détection Sage 100
        string[] baseDirs = new[]
        {
            @"C:\Program Files (x86)\Sage",
            @"C:\Program Files\Sage"
        };

        var sage100 = DetectApp("Maestria.exe", baseDirs);

        // Détection Objets Métiers
        string? omDllPath = null;
        var p1 = @"C:\Program Files (x86)\Common Files\Sage\Objets métiers\objets100c.dll";
        var p2 = @"C:\Program Files\Common Files\Sage\Objets métiers\objets100c.dll";
        if (File.Exists(p1)) omDllPath = p1;
        else if (File.Exists(p2)) omDllPath = p2;

        bool omInstalled = omDllPath != null;
        string? omVersion = null;

        if (omInstalled && File.Exists(omDllPath!))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(omDllPath);
                omVersion = vi.FileVersion;
            }
            catch { }
        }

        // Interop = COM Dynamique maintenant
        return Results.Json(new
        {
            sage100,
            om = new
            {
                installed = omInstalled,
                path = omDllPath,
                version = omVersion
            },
            interop = new
            {
                installed = true, // Toujours installé (COM dynamique)
                path = "COM Dynamique (Runtime)",
                version = "Auto-Detect"
            }
        });
    }

    private static IResult RegenerateInteropAsync(HttpContext ctx)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        try
        {
            // Utiliser AutoInteropGenerator pour générer/régénérer l'interop
            var interopGenerator = ctx.RequestServices.GetRequiredService<AutoInteropGenerator>();
            var result = interopGenerator.GenerateOrGetInterop();

            if (result.Success)
            {
                return Results.Json(new
                {
                    success = true,
                    message = $"✅ Interop généré/récupéré avec succès : {Path.GetFileName(result.InteropPath)}",
                    interopPath = result.InteropPath,
                    comDllPath = result.ComDllPath,
                    comDllVersion = result.ComDllVersion
                });
            }
            else
            {
                return Results.Json(new
                {
                    success = false,
                    message = $"❌ Erreur lors de la génération de l'interop : {result.Message}"
                }, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        catch (Exception ex)
        {
            return Results.Json(new
            {
                success = false,
                message = $"❌ Erreur inattendue : {ex.Message}"
            }, statusCode: StatusCodes.Status500InternalServerError);
        }
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
            return new { installed = false, path = (string?)null };

        try
        {
            var vi = FileVersionInfo.GetVersionInfo(found);
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
            return new { installed = true, path = found };
        }
    }

    private static bool CheckCsrf(HttpContext ctx)
    {
        var cookie = ctx.Request.Cookies["EXADM-CSRF"];
        var header = ctx.Request.Headers.TryGetValue("X-CSRF", out var hv) ? hv.ToString() : null;
        return !string.IsNullOrWhiteSpace(cookie) && string.Equals(header, cookie, StringComparison.Ordinal);
    }
}
