using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Lesezugriff auf die archivierten Prüfprotokolle (Historie).
/// </summary>
public class ProtocolService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ProtocolService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Zeile der Protokollübersicht.</summary>
    public record ProtocolListItem(
        int Id,
        DateTime CreatedAt,
        DateTime CompletedDate,
        int ArticleId,
        string ArticleIdentification,
        string? CategoryName,
        string FormName,
        int FormVersionNumber,
        InspectionResult Result,
        bool IsUnplanned,
        string? CreatedByName);

    public async Task<List<ProtocolListItem>> GetListAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionProtocols
            .Include(p => p.Article).ThenInclude(a => a.Category)
            .Include(p => p.FormVersion).ThenInclude(v => v.Form)
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProtocolListItem(
                p.Id,
                p.CreatedAt,
                p.CompletedDate,
                p.ArticleId,
                p.Article.Identification,
                p.Article.Category != null ? p.Article.Category.Name : null,
                p.FormVersion.Form.Name,
                p.FormVersion.VersionNumber,
                p.Result,
                p.IsUnplanned,
                // Snapshot-Name bleibt auch nach Benutzer-Löschung erhalten.
                p.CreatedByUserName ?? (p.CreatedByUser != null
                    ? p.CreatedByUser.FirstName + " " + p.CreatedByUser.LastName
                    : null)))
            .ToListAsync();
    }

    /// <summary>Vollständiges Protokoll für die Detailansicht inkl. erfasster Werte.</summary>
    public async Task<InspectionProtocol?> GetDetailAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionProtocols
            .AsNoTracking()
            .Include(p => p.Article)
            .Include(p => p.CreatedByUser)
            .Include(p => p.FormVersion).ThenInclude(v => v.Form)
            .Include(p => p.FieldValues).ThenInclude(fv => fv.FormField)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}
