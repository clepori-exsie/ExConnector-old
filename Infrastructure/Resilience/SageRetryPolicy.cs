using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace ExConnector.Infrastructure.Resilience;

/// <summary>
/// Politique de retry pour les opérations Sage 100 (Objets Métiers)
/// </summary>
public static class SageRetryPolicy
{
    /// <summary>
    /// Crée une politique de retry avec backoff exponentiel pour les opérations Sage
    /// </summary>
    public static AsyncRetryPolicy CreatePolicy(ILogger logger, int maxRetries = 3)
    {
        return Policy
            .Handle<System.Runtime.InteropServices.COMException>() // Erreurs COM
            .Or<InvalidOperationException>() // Erreurs Sage
            .Or<TimeoutException>() // Timeouts
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Tentative {RetryCount}/{MaxRetries} après {Delay}s | Opération: {Operation}",
                        retryCount, maxRetries, timeSpan.TotalSeconds, context.OperationKey
                    );
                }
            );
    }

    /// <summary>
    /// Crée une politique de retry simple (sans backoff) pour les opérations rapides
    /// </summary>
    public static AsyncRetryPolicy CreateSimplePolicy(ILogger logger, int maxRetries = 2)
    {
        return Policy
            .Handle<System.Runtime.InteropServices.COMException>()
            .Or<InvalidOperationException>()
            .RetryAsync(
                retryCount: maxRetries,
                onRetry: (exception, retryCount) =>
                {
                    logger.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} | Opération Sage",
                        retryCount, maxRetries
                    );
                }
            );
    }

}

