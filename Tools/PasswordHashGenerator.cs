using ExConnector.Core.Interfaces;
using ExConnector.Infrastructure.Security;

namespace ExConnector.Tools;

/// <summary>
/// Utilitaire pour générer un hash BCrypt d'un mot de passe
/// Utilisation : appel direct depuis Program.cs ou via endpoint admin
/// </summary>
public static class PasswordHashGenerator
{
    /// <summary>
    /// Génère un hash BCrypt pour le mot de passe fourni
    /// </summary>
    public static string Generate(string plainPassword)
    {
        IPasswordHasher hasher = new PasswordHasher();
        return hasher.HashPassword(plainPassword);
    }

    /// <summary>
    /// Affiche le hash d'un mot de passe dans la console (pour tests)
    /// </summary>
    public static void PrintHash(string plainPassword)
    {
        var hash = Generate(plainPassword);
        Console.WriteLine("========================================");
        Console.WriteLine("HASH BCrypt généré :");
        Console.WriteLine(hash);
        Console.WriteLine("========================================");
        Console.WriteLine("Ajoutez cette ligne dans exconnector.settings.json :");
        Console.WriteLine($"\"PasswordHash\": \"{hash}\"");
        Console.WriteLine("========================================");
    }
}

