using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace ExConnector.Middleware;

/// <summary>
/// Middleware simple de rate limiting pour protéger les endpoints sensibles (login)
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, LoginAttemptTracker> _attempts = new();
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private static DateTime _lastCleanup = DateTime.UtcNow;

    // Configuration
    private const int MaxAttemptsPerMinute = 5;
    private const int LockoutDurationMinutes = 5;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Nettoyage périodique
        CleanupOldEntries();

        // Rate limiting uniquement sur les endpoints de login
        if (context.Request.Path.StartsWithSegments("/admin/auth/connexion") ||
            context.Request.Path.StartsWithSegments("/admin/auth/login"))
        {
            var clientIp = GetClientIp(context);
            var tracker = _attempts.GetOrAdd(clientIp, _ => new LoginAttemptTracker());

            if (tracker.IsLockedOut())
            {
                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync($"{{\"error\": \"Trop de tentatives. Réessayez dans {tracker.GetRemainingLockoutSeconds()} secondes.\"}}");
                return;
            }

            // Capturer la réponse
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Si échec d'authentification (401), incrémenter les tentatives
            if (context.Response.StatusCode == 401)
            {
                tracker.RecordFailedAttempt();
            }
            else if (context.Response.StatusCode == 200)
            {
                tracker.Reset();
            }

            // Copier la réponse originale
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
        else
        {
            await _next(context);
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        // Essayer X-Forwarded-For (si derrière un proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void CleanupOldEntries()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
            return;

        _lastCleanup = DateTime.UtcNow;

        var keysToRemove = _attempts
            .Where(kvp => kvp.Value.IsExpired())
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _attempts.TryRemove(key, out _);
        }
    }

    private class LoginAttemptTracker
    {
        private readonly List<DateTime> _recentAttempts = new();
        private DateTime? _lockedUntil;

        public void RecordFailedAttempt()
        {
            lock (_recentAttempts)
            {
                var now = DateTime.UtcNow;
                _recentAttempts.Add(now);

                // Garder seulement les tentatives de la dernière minute
                _recentAttempts.RemoveAll(t => (now - t).TotalMinutes > 1);

                if (_recentAttempts.Count >= MaxAttemptsPerMinute)
                {
                    _lockedUntil = now.AddMinutes(LockoutDurationMinutes);
                }
            }
        }

        public bool IsLockedOut()
        {
            if (_lockedUntil == null)
                return false;

            if (DateTime.UtcNow < _lockedUntil.Value)
                return true;

            // Lockout expiré, réinitialiser
            _lockedUntil = null;
            lock (_recentAttempts)
            {
                _recentAttempts.Clear();
            }
            return false;
        }

        public int GetRemainingLockoutSeconds()
        {
            if (_lockedUntil == null)
                return 0;

            var remaining = (_lockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            return (int)Math.Max(0, remaining);
        }

        public void Reset()
        {
            lock (_recentAttempts)
            {
                _recentAttempts.Clear();
            }
            _lockedUntil = null;
        }

        public bool IsExpired()
        {
            // Considérer comme expiré si pas de lockout et pas de tentatives récentes depuis 30 minutes
            if (_lockedUntil != null)
                return false;

            lock (_recentAttempts)
            {
                if (_recentAttempts.Count == 0)
                    return true;

                return (DateTime.UtcNow - _recentAttempts.Last()).TotalMinutes > 30;
            }
        }
    }
}

