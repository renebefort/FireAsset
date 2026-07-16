using System.Diagnostics;
using System.Reflection;

namespace FireAsset.Services;

/// <summary>
/// Liefert Eckdaten der Anwendung für die "Über"-Seite: Version (neuestes Git-Tag, sonst
/// Assembly-Version), Build-Datum (Zeitstempel der Assembly-Datei) und die Repository-URL.
/// </summary>
public class AppInfoService
{
    public const string RepositoryUrl = "https://github.com/renebefort/FireAsset";

    private readonly IWebHostEnvironment _env;

    public AppInfoService(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>Neuestes Git-Tag; Fallback auf die Assembly-Version, wenn kein Git/Tag vorhanden.</summary>
    public async Task<string> GetVersionAsync()
    {
        var tag = await RunGitAsync("describe --tags --abbrev=0");
        if (!string.IsNullOrWhiteSpace(tag))
        {
            return tag.Trim();
        }
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "unbekannt" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>Build-/Bereitstellungsdatum aus dem Zeitstempel der Assembly-Datei; null bei Fehler.</summary>
    public DateTime? GetBuildDate()
    {
        try
        {
            var location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(location) || !File.Exists(location))
            {
                return null;
            }
            return File.GetLastWriteTime(location);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Führt einen Git-Befehl im Anwendungsverzeichnis aus; null bei Fehler/fehlendem Git.</summary>
    private async Task<string?> RunGitAsync(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _env.ContentRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
