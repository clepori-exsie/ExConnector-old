using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Point d'entrée pour tous les endpoints d'administration
/// </summary>
public static class AdminEndpoints
{
    /// <summary>
    /// Enregistre tous les endpoints d'administration
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(
        this IEndpointRouteBuilder app,
        string logDir,
        string adminRoot,
        string[] listenUrls)
    {
        // Authentification
        app.MapAuthEndpoints();

        // Configuration
        app.MapConfigEndpoints();

        // Gestion des dossiers Sage
        app.MapSageFoldersEndpoints();

        // Gestion Interop
        app.MapInteropEndpoints();

        // Logs
        app.MapLogsEndpoints(logDir);

        // Utilitaires
        app.MapUtilityEndpoints();

        // Version Sage
        app.MapSageVersionEndpoints();

        return app;
    }
}

