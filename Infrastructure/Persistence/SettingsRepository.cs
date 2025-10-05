using System.Text;
using System.Text.Json;
using ExConnector.Core.Interfaces;

namespace ExConnector.Infrastructure.Persistence;

/// <summary>
/// Gestion de la persistence des paramètres de configuration
/// </summary>
public class SettingsRepository : ISettingsRepository
{
    private static readonly object _lock = new();
    private string _filePath = Path.Combine(AppContext.BaseDirectory, "exconnector.settings.json");

    public string FilePath => _filePath;

    public void Initialize(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Chemin de fichier invalide", nameof(filePath));

        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, "{}\n", Encoding.UTF8);
        }
    }

    public void SavePatch(Dictionary<string, object?> patch)
    {
        lock (_lock)
        {
            var current = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    current = ToDictionary(doc.RootElement);
                }
            }

            foreach (var kv in patch)
                current[kv.Key] = kv.Value;

            var text = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, text + Environment.NewLine, Encoding.UTF8);
        }
    }

    private static Dictionary<string, object?> ToDictionary(JsonElement el)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (el.ValueKind != JsonValueKind.Object) return dict;
        foreach (var p in el.EnumerateObject())
            dict[p.Name] = ToNet(p.Value);
        return dict;
    }

    private static object? ToNet(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => ToDictionary(el),
            JsonValueKind.Array => el.EnumerateArray().Select(ToNet).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l :
                                    el.TryGetDouble(out var d) ? d : el.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}

