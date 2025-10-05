namespace ExConnector.Infrastructure.Sage;

/// <summary>
/// Gestionnaire de threads STA pour les appels COM
/// Repris du backup original pour la compatibilité
/// </summary>
public static class StaRunner
{
    /// <summary>
    /// Exécute une fonction dans un thread STA (Single Threaded Apartment)
    /// Nécessaire pour les appels COM
    /// </summary>
    public static Task<T> RunAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        
        return tcs.Task;
    }

    /// <summary>
    /// Exécute une action dans un thread STA
    /// </summary>
    public static Task RunAsync(Action action)
    {
        return RunAsync(() =>
        {
            action();
            return true;
        });
    }
}