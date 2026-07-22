using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace Cardex.Services;

public record UpdateInfo(string Version, string DownloadUrl);

public class UpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/Darumacho/Cardex-Companion/releases/latest";
    private const string AssetName = "Cardex.exe";

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

            var assets = root.GetProperty("assets");
            string? downloadUrl = null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == AssetName)
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            return downloadUrl is not null ? new UpdateInfo(tag, downloadUrl) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAndInstallAsync(UpdateInfo info, IProgress<int>? progress = null)
    {
        var currentExe = Process.GetCurrentProcess().MainModule!.FileName;
        var tempExe = Path.Combine(Path.GetTempPath(), "Cardex_update.exe");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Cardex-Companion");

        using var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var src = await response.Content.ReadAsStreamAsync();
        await using var dst = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

        var buf = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buf)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((int)(read * 100 / total));
        }

        var script = Path.Combine(Path.GetTempPath(), "cardex_update.ps1");
        await File.WriteAllTextAsync(script, $$"""
            $proc = Get-Process -Id {{Environment.ProcessId}} -ErrorAction SilentlyContinue
            if ($proc) { $proc.WaitForExit(30000) }
            Start-Sleep -Milliseconds 500
            Copy-Item -LiteralPath '{{tempExe}}' -Destination '{{currentExe}}' -Force
            Start-Process -FilePath '{{currentExe}}'
            Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
            """);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
    }
}
