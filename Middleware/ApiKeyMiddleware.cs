using ExConnector.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ExConnector.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _cfg;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration cfg)
    {
        _next = next; _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            await _next(context);
            return;
        }

        var expected = _cfg.GetSection("Api").Get<ApiConfig>()?.ApiKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Clé API non configurée (Api:ApiKey)." });
            return;
        }

        // Nouveau nom d’en-tête + compat ancien
        if (!context.Request.Headers.TryGetValue("X-ExConnector-ApiKey", out var key) &&
            !context.Request.Headers.TryGetValue("X-API-Key", out key))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        if (!string.Equals(key.ToString(), expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }
}
