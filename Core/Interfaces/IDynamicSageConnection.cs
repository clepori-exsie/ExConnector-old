namespace ExConnector.Core.Interfaces;

/// <summary>
/// Interface pour une connexion COM dynamique à Sage 100
/// </summary>
public interface IDynamicSageConnection : IDisposable
{
    /// <summary>
    /// Indique si la connexion est ouverte
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Ouvre la connexion
    /// </summary>
    void Open();

    /// <summary>
    /// Ferme la connexion
    /// </summary>
    void Close();

    /// <summary>
    /// Accès dynamique à l'objet COM Sage
    /// </summary>
    dynamic Instance { get; }
}

