using System.Security.Cryptography;
using System.Text;
using ExConnector.Core.Interfaces;

namespace ExConnector.Infrastructure.Security;

/// <summary>
/// Service de chiffrement avec Windows DPAPI (Data Protection API)
/// </summary>
public class DpapiDataProtectionService : IDataProtectionService
{
    private const string EncryptedPrefix = "ENC:";

    /// <summary>
    /// Chiffre une chaîne avec DPAPI
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return plainText;

        // Si déjà chiffré, retourner tel quel
        if (IsEncrypted(plainText))
            return plainText;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine // Accessible par tous les users de la machine
        );

        return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Déchiffre une chaîne chiffrée avec DPAPI
    /// </summary>
    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrWhiteSpace(encryptedText))
            return encryptedText;

        // Si pas chiffré, retourner tel quel
        if (!IsEncrypted(encryptedText))
            return encryptedText;

        try
        {
            var base64 = encryptedText[EncryptedPrefix.Length..];
            var encryptedBytes = Convert.FromBase64String(base64);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.LocalMachine
            );

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            throw new CryptographicException($"Échec du déchiffrement DPAPI : {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Vérifie si une valeur est déjà chiffrée (préfixe "ENC:")
    /// </summary>
    public bool IsEncrypted(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }
}

