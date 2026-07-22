using System.IO;
using System.Text.Json;

namespace Cardex.Services;

public class AppSettings
{
    public string? ApiKey { get; set; }

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Cardex", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), _json) ?? new();
        }
        catch { }
        return new();
    }
}
