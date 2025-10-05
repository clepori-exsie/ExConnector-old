using System.Net;
using ExConnector.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ExConnector.Middleware;

public class AdminSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _cfg;

    private static readonly HashSet<string> PublicAdminPaths = new(StringComparer.OrdinalIgnoreCase)
    {
    "/admin/connexion.html",
    "/admin/style.css",
    "/admin/common.js",
    "/admin/assets/logo.svg",

    "/admin/auth/connexion",
    "/admin/auth/login",
    "/admin/auth/deconnexion",
    "/admin/auth/logout",

    "/admin" // la racine (redirige ensuite)
    };


    public AdminSessionMiddleware(RequestDelegate next, IConfiguration cfg)
    {
        _next = next; _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.ToString();

        // Localhost-only pour /admin si demandé
        var adminCfg = _cfg.GetSection("Admin").Get<AdminConfig>() ?? new();
        if (adminCfg.LocalOnly)
        {
            var ip = ctx.Connection.RemoteIpAddress;
            bool isLocal = ip is not null && (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));
            if (!isLocal)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "Admin accessible uniquement en localhost" });
                return;
            }
        }

        // Laisse passer la page de connexion et les assets publics
        if (PublicAdminPaths.Contains(path) || path.StartsWith("/admin/assets/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // Pour toutes les autres ressources /admin → auth cookie requis
        var token = ctx.Request.Cookies["EXADM"];
        if (string.IsNullOrWhiteSpace(token))
        {
            if (ctx.Request.Headers.TryGetValue("Accept", out var acc) && acc.ToString().Contains("text/html"))
            {
                ctx.Response.Redirect("/admin/connexion.html");
                return;
            }
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(ctx);
    }
}
