using System.Diagnostics;
using System.Text.Json;
using ExConnector.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints utilitaires (ouvrir dossiers, etc.)
/// </summary>
public static class UtilityEndpoints
{
    public static IEndpointRouteBuilder MapUtilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/api/open-folder", OpenFolderAsync);

        return app;
    }

    private static IResult OpenFolderAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Results.BadRequest(new { error = "Chemin manquant" });

            if (!Directory.Exists(path))
                return Results.BadRequest(new { error = "Dossier introuvable" });

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });

            return Results.Json(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

