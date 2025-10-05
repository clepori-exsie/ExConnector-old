using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ExConnector.API.Endpoints.Admin;

/// <summary>
/// Endpoints de consultation des logs
/// </summary>
public static class LogsEndpoints
{
    public static IEndpointRouteBuilder MapLogsEndpoints(
        this IEndpointRouteBuilder app,
        string logDir)
    {
        app.MapGet("/admin/api/logs", (int tail = 200) => GetLogsAsync(logDir, tail));
        app.MapGet("/admin/api/logs/download", () => DownloadLogsAsync(logDir));

        return app;
    }

    private static IResult GetLogsAsync(string logDir, int tail)
    {
        if (tail <= 0) tail = 200;
        var file = Path.Combine(logDir, $"exconnector-{DateTime.Now:yyyyMMdd}.log");
        if (!File.Exists(file))
            return Results.Json(new { file, lines = Array.Empty<string>() });

        var lines = File.ReadLines(file).Reverse().Take(tail).Reverse().ToArray();
        return Results.Json(new { file, lines });
    }

    private static IResult DownloadLogsAsync(string logDir)
    {
        var file = Path.Combine(logDir, $"exconnector-{DateTime.Now:yyyyMMdd}.log");
        if (!File.Exists(file))
            return Results.NotFound(new { error = "Log introuvable" });

        var bytes = File.ReadAllBytes(file);
        return Results.File(bytes, "text/plain", Path.GetFileName(file));
    }
}

