namespace ExConnector.Core.Interfaces;

/// <summary>
/// Service de hachage de mots de passe
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash un mot de passe en clair avec BCrypt
    /// </summary>
    string HashPassword(string plainPassword);

    /// <summary>
    /// Vérifie si le mot de passe en clair correspond au hash
    /// </summary>
    bool VerifyPassword(string plainPassword, string hashedPassword);
}

