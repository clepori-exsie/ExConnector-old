using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Sage;

/// <summary>
/// Point d'entrée pour tous les endpoints Sage
/// </summary>
public static class SageEndpoints
{
    /// <summary>
    /// Enregistre tous les endpoints Sage (Tiers, Articles, Documents, etc.)
    /// </summary>
    public static IEndpointRouteBuilder MapSageEndpoints(this IEndpointRouteBuilder app)
    {
        // Endpoints Tiers
        app.MapTiersEndpoints();

        // Futurs endpoints :
        // app.MapArticlesEndpoints();
        // app.MapDocumentsEndpoints();
        // app.MapStocksEndpoints();
        // app.MapComptabiliteEndpoints();
        // etc.

        return app;
    }
}

