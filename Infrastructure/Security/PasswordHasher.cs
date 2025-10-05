using ExConnector.Core.Interfaces;

namespace ExConnector.Infrastructure.Security;

/// <summary>
/// Implémentation BCrypt pour le hachage de mots de passe
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Hash un mot de passe avec BCrypt (workFactor = 12)
    /// </summary>
    public string HashPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            throw new ArgumentException("Le mot de passe ne peut pas être vide", nameof(plainPassword));

        return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
    }

    /// <summary>
    /// Vérifie un mot de passe contre son hash BCrypt
    /// </summary>
    public bool VerifyPassword(string plainPassword, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(hashedPassword))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
        }
        catch
        {
            return false;
        }
    }
}

