using System.Security.Cryptography;
using System.Text.Json;
using ExConnector.Core.Interfaces;
using ExConnector.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints d'authentification administrateur
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/auth/connexion", LoginAsync);
        app.MapPost("/admin/auth/login", LoginAsync);
        app.MapPost("/admin/auth/deconnexion", LogoutAsync);
        app.MapPost("/admin/auth/logout", LogoutAsync);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext ctx,
        IConfiguration cfg,
        IAdminTokenService tokens,
        IPasswordHasher passwordHasher)
    {
        var body = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(ctx.Request.Body) ?? new();
        var user = (body.GetValueOrDefault("user") ?? "").Trim();
        var pass = body.GetValueOrDefault("password") ?? "";

        // Récupération des credentials depuis la configuration
        var expectedUser = cfg["Admin:User"] ?? "ExConnector-Administrateur";
        var passwordHash = cfg["Admin:PasswordHash"];

        // Si pas de hash configuré, fallback sur l'ancien système (temporaire)
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            var oldPassword = cfg["Admin:Password"];
            if (!string.IsNullOrWhiteSpace(oldPassword) && user == expectedUser && pass == oldPassword)
            {
                // Authentification réussie avec l'ancien système
                var token = tokens.Create(user, TimeSpan.FromHours(8));
                SetAuthCookies(ctx, token, user);
                return Results.Json(new { ok = true, user, warning = "Utilisez un hash BCrypt pour plus de sécurité" });
            }
        }
        else
        {
            // Vérification avec BCrypt
            if (user == expectedUser && passwordHasher.VerifyPassword(pass, passwordHash))
            {
                var token = tokens.Create(user, TimeSpan.FromHours(8));
                SetAuthCookies(ctx, token, user);
                return Results.Json(new { ok = true, user });
            }
        }

        return Results.Unauthorized();
    }

    private static void SetAuthCookies(HttpContext ctx, string token, string user)
    {
        ctx.Response.Cookies.Append("EXADM", token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Path = "/admin"
        });

        var csrf = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        ctx.Response.Cookies.Append("EXADM-CSRF", csrf, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Path = "/admin"
        });
    }

    private static IResult LogoutAsync(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete("EXADM", new CookieOptions { Path = "/admin" });
        ctx.Response.Cookies.Delete("EXADM-CSRF", new CookieOptions { Path = "/admin" });
        return Results.Json(new { ok = true });
    }
}

