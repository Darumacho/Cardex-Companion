using System.Diagnostics;
using System.IO;
using Avalonia.Platform;

namespace Cardex.Platform;

// Avalonia n'a pas de lecteur audio (le WPF utilise un MediaElement) : sur macOS on passe par
// afplay, l'outil de lecture intégré au système. Les mp3 sont extraits une fois dans le dossier
// temporaire, comme côté WPF.
public static class SoundPlayer
{
    private static readonly Dictionary<string, string> _paths = new();
    private static Process? _current;

    // soundKey : "xbox", "ps4" ou un nom de fichier de Assets/ (sons personnalisés des succès).
    public static void Play(string soundKey)
    {
        var path = GetSoundPath(soundKey);
        if (path is null) return;

        try
        {
            if (_current is { HasExited: false }) _current.Kill();
            _current = Process.Start(new ProcessStartInfo("afplay", $"\"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch { }
    }

    private static string? GetSoundPath(string soundKey)
    {
        if (_paths.TryGetValue(soundKey, out var cached)) return cached;

        var fileName = soundKey switch
        {
            "xbox" => "AchievementXbox.mp3",
            "ps4"  => "AchievementPS4.mp3",
            _      => soundKey
        };
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://Cardex/Assets/{fileName}"));
            var path = Path.Combine(Path.GetTempPath(), $"Cardex_{fileName}");
            using (var fs = File.Create(path))
                stream.CopyTo(fs);
            _paths[soundKey] = path;
            return path;
        }
        catch { return null; }
    }
}
