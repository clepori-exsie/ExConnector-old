using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Health;

/// <summary>
/// Endpoints de health check
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(
        this IEndpointRouteBuilder app,
        string[] listenUrls)
    {
        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            service = "ExConnector",
            version = "2.0",
            timestamp = DateTime.UtcNow
        }));

        app.MapGet("/", () => Results.Json(new
        {
            service = "ExConnector - Sage 100 API Gateway",
            version = "2.0",
            status = "running",
            endpoints = new
            {
                health = "/health",
                tiers = "/tiers",
                admin = "/admin"
            },
            listenUrls
        }));

        return app;
    }
}

