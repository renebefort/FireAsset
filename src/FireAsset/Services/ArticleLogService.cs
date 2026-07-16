using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Schreibt und liest Logbuch-Einträge zu Artikeln (Anlage, Bearbeitung, Stilllegung,
/// Standortwechsel). Die Schreibmethoden fügen den Eintrag nur dem übergebenen DbContext hinzu
/// (kein eigenes Save) und laufen damit atomar in der auslösenden Operation mit.
/// </summary>
public class ArticleLogService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ArticleLogService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Zeile für das Logbuch-Grid.</summary>
    public record LogRow(int Id, string Article, ArticleLogAction Action, string? Details, string? User, DateTime Timestamp);

    /// <summary>Ermittelt den Anzeigenamen eines Benutzers (einmalig, z. B. vor einer Schleife).</summary>
    public async Task<string?> ResolveUserNameAsync(AppDbContext db, int? userId)
    {
        if (userId is not int uid) return null;
        return await db.Users
            .Where(u => u.Id == uid)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync();
    }

    /// <summary>Fügt einen Eintrag hinzu (kein Save). Nutzt die Artikel-Navigation, damit die FK
    /// auch bei einem neu angelegten Artikel beim gemeinsamen Save korrekt gesetzt wird.</summary>
    public void Add(AppDbContext db, Article article, ArticleLogAction action, string? details, int? userId, string? userName)
    {
        db.ArticleLogEntries.Add(new ArticleLogEntry
        {
            Article = article,
            ArticleIdentificationSnapshot = article.Identification,
            Action = action,
            Details = details,
            UserId = userId,
            UserNameSnapshot = userName,
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>Komfort für Einzeloperationen: löst den Benutzernamen auf und fügt den Eintrag hinzu (kein Save).</summary>
    public async Task LogAsync(AppDbContext db, Article article, ArticleLogAction action, string? details, int? userId)
    {
        var userName = await ResolveUserNameAsync(db, userId);
        Add(db, article, action, details, userId, userName);
    }

    public async Task<List<LogRow>> GetEntriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ArticleLogEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id)
            .Select(e => new LogRow(e.Id, e.ArticleIdentificationSnapshot, e.Action, e.Details, e.UserNameSnapshot, e.Timestamp))
            .ToListAsync();
    }

    /// <summary>Baut den Details-Text eines Standortwechsels ("Von „A“ nach „B“").</summary>
    public static string LocationChange(string? from, string? to) =>
        $"Von „{(string.IsNullOrWhiteSpace(from) ? "—" : from)}“ nach „{(string.IsNullOrWhiteSpace(to) ? "—" : to)}“";
}
