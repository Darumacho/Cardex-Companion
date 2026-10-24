using System.IO;

namespace Cardex.Services;

// Dossier des données locales (base SQLite, settings.json, cache d'images, backups de synchro).
// Windows : %AppData%\Cardex (inchangé). macOS : ~/Library/Application Support/Cardex, l'emplacement
// standard sur Mac — SpecialFolder.ApplicationData y pointe sinon vers ~/.config.
public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Cardex");
}
