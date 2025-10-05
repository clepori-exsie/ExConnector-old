using System.Security.Cryptography;
using System.Text;
using ExConnector.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ExConnector.Infrastructure.Security;

/// <summary>
/// Service de gestion des tokens d'administration
/// </summary>
public class AdminTokenService : IAdminTokenService
{
    private readonly byte[] _secret;

    public AdminTokenService(IConfiguration cfg)
    {
        var secret = cfg["Admin:TokenSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            var pwd = cfg["Admin:Password"] ?? "change-me";
            using var sha = SHA256.Create();
            _secret = sha.ComputeHash(Encoding.UTF8.GetBytes("EXC-" + pwd));
        }
        else
        {
            _secret = Encoding.UTF8.GetBytes(secret);
        }
    }

    public string Create(string user, TimeSpan ttl)
    {
        var exp = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var payload = $"{user}|{exp}|{nonce}";
        var sig = Sign(payload);
        return $"{Base64Url(payload)}.{Base64Url(sig)}";
    }

    public bool Validate(string token, out string? user)
    {
        user = null;
        var parts = token.Split('.', 2);
        if (parts.Length != 2) return false;

        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        var sig = Base64UrlDecode(parts[1]);
        if (!ConstantTimeEquals(sig, Sign(payload))) return false;

        var items = payload.Split('|');
        if (items.Length != 3) return false;

        user = items[0];
        if (!long.TryParse(items[1], out var exp)) return false;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;

        return true;
    }

    private byte[] Sign(string payload)
    {
        using var h = new HMACSHA256(_secret);
        return h.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64Url(string s) => Base64Url(Encoding.UTF8.GetBytes(s));
    private static string Base64Url(byte[] b) => 
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    
    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

