using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ExConnector.Middleware;

/// <summary>
/// Middleware global de gestion des exceptions
/// Capture toutes les exceptions non gérées et retourne une réponse JSON standardisée
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        var path = httpContext.Request.Path;
        var method = httpContext.Request.Method;

        // Log l'erreur avec contexte
        _logger.LogError(
            exception,
            "Exception non gérée | TraceId: {TraceId} | {Method} {Path}",
            traceId, method, path
        );

        // Déterminer le code de statut et le message
        var (statusCode, title, detail) = MapExceptionToResponse(exception);

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title,
            status = statusCode,
            detail,
            traceId,
            timestamp = DateTime.UtcNow
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails),
            cancellationToken
        );

        return true; // Exception gérée
    }

    private static (int StatusCode, string Title, string Detail) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            ArgumentNullException or ArgumentException => 
                (StatusCodes.Status400BadRequest, "Requête invalide", exception.Message),
            
            InvalidOperationException => 
                (StatusCodes.Status400BadRequest, "Opération invalide", exception.Message),
            
            UnauthorizedAccessException => 
                (StatusCodes.Status403Forbidden, "Accès refusé", "Vous n'avez pas les permissions nécessaires."),
            
            KeyNotFoundException or FileNotFoundException => 
                (StatusCodes.Status404NotFound, "Ressource introuvable", exception.Message),
            
            TimeoutException => 
                (StatusCodes.Status408RequestTimeout, "Délai d'attente dépassé", "L'opération a pris trop de temps."),
            
            NotImplementedException => 
                (StatusCodes.Status501NotImplemented, "Non implémenté", "Cette fonctionnalité n'est pas encore disponible."),
            
            _ => (StatusCodes.Status500InternalServerError, "Erreur serveur", 
                  "Une erreur interne s'est produite. Veuillez réessayer plus tard.")
        };
    }
}

