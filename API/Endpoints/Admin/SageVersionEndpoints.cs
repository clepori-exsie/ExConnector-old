using ExConnector.Infrastructure.Sage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints pour la gestion de la version Sage
/// </summary>
public static class SageVersionEndpoints
{
    public static IEndpointRouteBuilder MapSageVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/api/sage/version", (SageVersionDetector detector) =>
        {
            var version = detector.DetectVersion();
            
            return Results.Json(new
            {
                isInstalled = version.IsAvailable,
                productName = version.ProductName,
                fileVersion = version.FileVersion,
                productVersion = version.ProductVersion,
                path = version.FilePath,
                message = version.IsAvailable 
                    ? $"✅ {version}" 
                    : "❌ Sage 100 Objets Métiers non détecté"
            });
        })
        .WithName("GetSageVersion")
        .WithTags("Admin", "Sage", "Version");

        return app;
    }
}

