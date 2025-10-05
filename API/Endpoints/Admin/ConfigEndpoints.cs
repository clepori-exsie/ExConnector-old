using System.Text.Json;
using ExConnector.Core.Interfaces;
using ExConnector.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints de gestion de la configuration
/// </summary>
public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/api/config", GetConfigAsync);
        app.MapPost("/admin/api/api-key", SetApiKeyAsync);
        app.MapPost("/admin/api/sage100", SaveSageConfigAsync);
        app.MapPost("/admin/api/test-sage", TestSageConnectionAsync);

        return app;
    }

    private static IResult GetConfigAsync(IConfiguration cfg, IDataProtectionService dataProtection)
    {
        var api = cfg.GetSection("Api").Get<ApiConfig>() ?? new();
        var sage = cfg.GetSection("SageConfig").Get<SageConfig>() ?? new();
        var admin = cfg.GetSection("Admin").Get<AdminConfig>() ?? new();
        var logging = cfg.GetSection("Logging").Get<LoggingConfig>() ?? new();
        var listenUrls = api.ListenUrls ?? new[] { "http://localhost:14330" };

        static string Mask(string? v) => string.IsNullOrEmpty(v) ? "" : new string('*', Math.Min(8, v.Length));
        bool apiConfigured = !string.IsNullOrWhiteSpace(api.ApiKey);
        
        // Déchiffrer le mot de passe Sage pour affichage masqué
        string? sagePassword = sage.Password;
        if (!string.IsNullOrEmpty(sagePassword) && dataProtection.IsEncrypted(sagePassword))
        {
            try
            {
                sagePassword = dataProtection.Decrypt(sagePassword);
            }
            catch
            {
                sagePassword = null; // En cas d'erreur, masquer
            }
        }

        return Results.Json(new
        {
            Api = new { ApiKey = api.ApiKey ?? "", ApiConfigured = apiConfigured, ListenUrls = listenUrls },
            SageConfig = new { sage.MAEPath, sage.CompanyServer, sage.CompanyDatabaseName, UserName = sage.UserName, Password = Mask(sagePassword) },
            Admin = new { admin.User, Password = "***", admin.LocalOnly },
            Logging = new { Dir = logging.Dir ?? "logs" }
        });
    }

    private static async Task<IResult> SetApiKeyAsync(
        HttpContext ctx,
        ISettingsRepository settingsRepo)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var req = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(ctx.Request.Body) ?? new();
        var action = (req.GetValueOrDefault("action") ?? "").Trim().ToLowerInvariant();
        if (action != "set")
            return Results.BadRequest(new { error = "action invalide (utiliser 'set')" });

        var newKey = (req.GetValueOrDefault("value") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(newKey))
            return Results.BadRequest(new { error = "clé vide" });

        settingsRepo.SavePatch(new Dictionary<string, object?> { ["Api"] = new { ApiKey = newKey } });

        return Results.Json(new
        {
            ok = true,
            file = settingsRepo.FilePath,
            lastWrite = File.GetLastWriteTime(settingsRepo.FilePath)
        });
    }

    private static async Task<IResult> SaveSageConfigAsync(
        HttpContext ctx,
        ISettingsRepository settingsRepo,
        IDataProtectionService dataProtection)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var config = await JsonSerializer.DeserializeAsync<SageConfig>(ctx.Request.Body) ?? new();
        
        // Chiffrer le mot de passe Sage avec DPAPI si fourni et non déjà chiffré
        if (!string.IsNullOrWhiteSpace(config.Password) && !dataProtection.IsEncrypted(config.Password))
        {
            config.Password = dataProtection.Encrypt(config.Password);
        }
        
        settingsRepo.SavePatch(new Dictionary<string, object?> { ["SageConfig"] = config });

        return Results.Json(new { ok = true });
    }

    private static async Task<IResult> TestSageConnectionAsync(
        HttpContext ctx,
        Core.Interfaces.ISageService sageService)
    {
        if (!CheckCsrf(ctx))
            return Results.Json(new { error = "Forbidden (CSRF)" }, statusCode: StatusCodes.Status403Forbidden);

        var result = await sageService.PingAsync();
        return Results.Json(result);
    }

    private static bool CheckCsrf(HttpContext ctx)
    {
        var cookie = ctx.Request.Cookies["EXADM-CSRF"];
        var header = ctx.Request.Headers.TryGetValue("X-CSRF", out var hv) ? hv.ToString() : null;
        return !string.IsNullOrWhiteSpace(cookie) && string.Equals(header, cookie, StringComparison.Ordinal);
    }
}

