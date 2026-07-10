using System.Diagnostics;

namespace FireAsset.Services;

/// <summary>
/// Liest das Änderungsprotokoll aus dem Git-Repository: Commit-Messages, gruppiert nach
/// Release-Tags (neuester Release zuerst, Commits nach dem letzten Tag als "Unveröffentlicht").
/// Steht kein Git-Repository zur Verfügung (z. B. im Deployment), bleibt die Liste leer.
/// </summary>
public class ChangelogService
{
    private readonly IWebHostEnvironment _env;

    public ChangelogService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public record Entry(string Hash, DateTime Date, string Subject);

    public record Group(string Title, DateTime? Date, List<Entry> Entries);

    public async Task<List<Group>> GetChangelogAsync()
    {
        var tagsRaw = await RunGitAsync("tag --list --sort=creatordate --format=%(refname:short)|%(creatordate:iso-strict)");
        if (tagsRaw is null)
        {
            return new List<Group>();
        }

        var tags = tagsRaw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2))
            .Where(parts => parts.Length == 2 && DateTime.TryParse(parts[1], out _))
            .Select(parts => (Name: parts[0], Date: DateTime.Parse(parts[1])))
            .ToList();

        var groups = new List<Group>();
        string? previousTag = null;
        foreach (var tag in tags)
        {
            var range = previousTag is null ? tag.Name : $"{previousTag}..{tag.Name}";
            groups.Add(new Group(tag.Name, tag.Date, await GetEntriesAsync(range)));
            previousTag = tag.Name;
        }

        var unreleased = await GetEntriesAsync(previousTag is null ? "HEAD" : $"{previousTag}..HEAD");
        if (unreleased.Count > 0)
        {
            groups.Add(new Group("Unveröffentlicht", null, unreleased));
        }

        groups.Reverse(); // neuester Release zuerst
        return groups;
    }

    private async Task<List<Entry>> GetEntriesAsync(string range)
    {
        var log = await RunGitAsync($"log {range} --pretty=format:%h|%cI|%s");
        if (log is null)
        {
            return new List<Entry>();
        }

        return log
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 3))
            .Where(parts => parts.Length == 3 && DateTime.TryParse(parts[1], out _))
            .Select(parts => new Entry(parts[0], DateTime.Parse(parts[1]), parts[2]))
            .ToList();
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
            return null; // Git nicht installiert, kein Repository o. ä. – Seite zeigt dann einen Hinweis.
        }
    }
}
