using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Cardex.Services;

// DownloadUrl : le zip de l'asset macOS ; PageUrl : la page de la release (repli).
public record UpdateInfo(string Version, string DownloadUrl, string PageUrl);

// Version macOS de Services/UpdateService.cs. Même principe que côté Windows : la release GitHub
// la plus récente porte un fichier dédié au Mac (Cardex-macOS-<arch>.zip, contenant Cardex.app,
// produit par _macOS/publish-mac.sh) ; on le télécharge, puis un script remplace le bundle .app
// après la fermeture de l'app et la relance (équivalent du script PowerShell Windows).
public class UpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/Darumacho/Cardex-Companion/releases/latest";

    // Nom de l'asset attendu dans la release, selon l'architecture du Mac.
    public static string AssetName =>
        RuntimeInformation.OSArchitecture == Architecture.X64 ? "Cardex-macOS-x64.zip" : "Cardex-macOS-arm64.zip";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Cardex-Companion");
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var versionStr = tag.TrimStart('v');
            if (!Version.TryParse(versionStr, out var remote)) return null;

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (current is not null)
            {
                // Normalize to Major.Minor.Build to avoid -1 revision comparison issues
                var r = new Version(remote.Major, Math.Max(0, remote.Minor), Math.Max(0, remote.Build));
                var c = new Version(current.Major, Math.Max(0, current.Minor), Math.Max(0, current.Build));
                if (r <= c) return null;
            }

            // Comme côté Windows : pas d'asset pour cette plateforme → pas de mise à jour proposée
            // (une release publiée sans build Mac n'affiche pas le bandeau sur Mac).
            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == AssetName)
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            var pageUrl = root.GetProperty("html_url").GetString() ?? "";
            return downloadUrl is not null ? new UpdateInfo(tag, downloadUrl, pageUrl) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAndInstallAsync(UpdateInfo info, IProgress<int>? progress = null)
    {
        // Remplacement automatique impossible (lancée via `dotnet run`, ou dossier en lecture
        // seule) : on ouvre la page de la release pour un téléchargement manuel.
        var bundle = CurrentAppBundle();
        if (bundle is null || !CanReplace(bundle))
        {
            Process.Start(new ProcessStartInfo(info.PageUrl) { UseShellExecute = true });
            progress?.Report(100);
            return;
        }

        var workDir = Path.Combine(Path.GetTempPath(), $"Cardex_update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, AssetName);

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Cardex-Companion");
            using var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                read += n;
                if (total > 0) progress?.Report((int)(read * 100 / total));
            }
        }

        var script = Path.Combine(workDir, "cardex_update.sh");
        await File.WriteAllTextAsync(script, BuildInstallScript(Environment.ProcessId, bundle, zipPath, workDir));

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/zsh",
            ArgumentList = { script },
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Dispatcher.UIThread.Post(() =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }

    // Script exécuté après la fermeture de l'app : extrait le nouveau Cardex.app (ditto conserve
    // la structure du bundle et sa signature), remplace l'ancien en le gardant de côté jusqu'au
    // succès (restauré en cas d'échec), puis relance l'app et nettoie.
    internal static string BuildInstallScript(int pid, string bundle, string zipPath, string workDir) => $$"""
        #!/bin/zsh
        APP={{Quote(bundle)}}
        ZIP={{Quote(zipPath)}}
        WORK={{Quote(workDir)}}

        # Attend la fermeture de l'app (30 s max)
        for i in {1..60}; do kill -0 {{pid}} 2>/dev/null || break; sleep 0.5; done
        sleep 0.5

        if ditto -x -k "$ZIP" "$WORK/extracted" && [ -d "$WORK/extracted/Cardex.app" ]; then
            rm -rf "$APP.old"
            if mv "$APP" "$APP.old" && ditto "$WORK/extracted/Cardex.app" "$APP"; then
                rm -rf "$APP.old"
            else
                rm -rf "$APP"
                mv "$APP.old" "$APP"
            fi
        fi

        open "$APP"
        rm -rf "$WORK"
        """;

    // Bundle .app contenant l'exécutable courant (…/Cardex.app/Contents/MacOS/Cardex), ou null
    // si l'app ne tourne pas depuis un bundle.
    internal static string? CurrentAppBundle()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return null;
        var bundle = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(exe)));
        return bundle is not null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(Path.Combine(bundle, "Contents", "MacOS"))
            ? bundle
            : null;
    }

    // Le bundle est remplacé par renommage : il faut pouvoir écrire dans le dossier qui le contient.
    private static bool CanReplace(string bundle)
    {
        var parent = Path.GetDirectoryName(bundle);
        if (parent is null) return false;
        try
        {
            var probe = Path.Combine(parent, $".cardex_write_test_{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Quote(string s) => "'" + s.Replace("'", "'\\''") + "'";
}
