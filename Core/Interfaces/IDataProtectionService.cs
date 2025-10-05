namespace ExConnector.Core.Interfaces;

/// <summary>
/// Service de chiffrement/déchiffrement avec DPAPI
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// Chiffre une chaîne avec DPAPI (Windows Data Protection)
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Déchiffre une chaîne chiffrée avec DPAPI
    /// </summary>
    string Decrypt(string encryptedText);

    /// <summary>
    /// Vérifie si une valeur est déjà chiffrée (commence par "ENC:")
    /// </summary>
    bool IsEncrypted(string? value);
}

