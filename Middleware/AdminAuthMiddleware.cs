using ExConnector.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;

namespace ExConnector.Middleware;

public class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _cfg;

    public AdminAuthMiddleware(RequestDelegate next, IConfiguration cfg)
    {
        _next = next;
        _cfg = cfg;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var admin = _cfg.GetSection("Admin").Get<AdminConfig>() ?? new();

        // Localhost only ?
        if (admin.LocalOnly)
        {
            var ip = ctx.Connection.RemoteIpAddress;
            var isLocal = ip is not null && (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));
            if (!isLocal)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { error = "Admin accessible uniquement en localhost" });
                return;
            }
        }

        // Basic Auth
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var auth) ||
            !auth.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ExConnector Admin\"";
            await ctx.Response.WriteAsync("Authentication required");
            return;
        }

        try
        {
            var token = auth.ToString().Substring("Basic ".Length).Trim();
            var creds = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = creds.Split(':', 2);
            if (parts.Length != 2 || parts[0] != (admin.User ?? "") || parts[1] != (admin.Password ?? ""))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ExConnector Admin\"";
                await ctx.Response.WriteAsync("Invalid credentials");
                return;
            }
        }
        catch
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.Headers["WWW-Authenticate"] = "Basic realm=\"ExConnector Admin\"";
            await ctx.Response.WriteAsync("Invalid auth header");
            return;
        }

        await _next(ctx);
    }
}
