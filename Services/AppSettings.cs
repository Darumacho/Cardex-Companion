using System.IO;
using System.Text.Json;

namespace Cardex.Services;

public class AppSettings
{
    public string? ApiKey { get; set; }
    public string CollectionBorderColor { get; set; } = "#3a7fc1";
    public bool ShowMyCollection { get; set; } = true;

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

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, _json));
        }
        catch { }
    }
}
