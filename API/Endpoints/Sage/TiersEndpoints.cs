using ExConnector.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Sage;

/// <summary>
/// Endpoints pour la gestion des tiers Sage
/// </summary>
public static class TiersEndpoints
{
    public static IEndpointRouteBuilder MapTiersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tiers")
            .WithTags("Tiers");

        group.MapGet("", GetTiersListAsync)
            .WithName("GetTiers")
            .WithSummary("Récupère une liste de tiers avec pagination");

        group.MapGet("/{numero}", GetTiersByNumeroAsync)
            .WithName("GetTiersByNumero")
            .WithSummary("Récupère un tiers par son numéro");

        return app;
    }

    private static async Task<IResult> GetTiersListAsync(
        HttpContext http,
        ITiersService tiersService,
        string? type = null,
        bool actifsOnly = false,
        int skip = 0,
        int take = 50)
    {
        try
        {
            var result = await tiersService.GetTiersAsync(type, actifsOnly, skip, take);
            return Results.Json(new
            {
                total = result.Total,
                skip,
                take = result.Items.Count,
                items = result.Items
            });
        }
        catch (Exception ex)
        {
            http.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Results.Json(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetTiersByNumeroAsync(
        string numero,
        ITiersService tiersService)
    {
        var tiers = await tiersService.GetTiersByNumeroAsync(numero);
        return tiers is null
            ? Results.NotFound(new { error = "Tiers introuvable." })
            : Results.Json(tiers);
    }
}

