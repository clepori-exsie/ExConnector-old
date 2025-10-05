using ExConnector.API.Endpoints.Admin;
using ExConnector.API.Endpoints.Health;
using ExConnector.API.Endpoints.Sage;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Extensions;

/// <summary>
/// Extensions pour mapper les endpoints
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Enregistre tous les endpoints de l'API
    /// </summary>
    public static IEndpointRouteBuilder MapApiEndpoints(
        this IEndpointRouteBuilder app,
        string logDir,
        string adminRoot,
        string[] listenUrls)
    {
        // Endpoints publics (protégés par clé API)
        app.MapHealthEndpoints(listenUrls);
        app.MapSageEndpoints();
        
        // Endpoints d'administration (protégés par session)
        app.MapAdminEndpoints(logDir, adminRoot, listenUrls);

        return app;
    }
}

