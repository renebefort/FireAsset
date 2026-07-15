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

    /// <summary>Vollständiges Protokoll für die Detailansicht inkl. erfasster Werte und aller Felder
    /// der Formularversion (auch Anzeige-Typen ohne Wert).</summary>
    public async Task<InspectionProtocol?> GetDetailAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionProtocols
            .AsNoTracking()
            .Include(p => p.Article)
            .Include(p => p.CreatedByUser)
            .Include(p => p.FormVersion).ThenInclude(v => v.Form)
            .Include(p => p.FormVersion).ThenInclude(v => v.Fields)
            .Include(p => p.FieldValues).ThenInclude(fv => fv.FormField)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Metadaten der Anhänge eines Protokolls (ohne Blob, für die Detailanzeige).</summary>
    public record AttachmentMeta(int FieldId, string FileName, string ContentType, long SizeBytes);

    public async Task<List<AttachmentMeta>> GetAttachmentMetasAsync(int protocolId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ProtocolFieldAttachments
            .Where(a => a.ProtocolId == protocolId)
            .Select(a => new AttachmentMeta(a.FormFieldId, a.FileName, a.ContentType, a.SizeBytes))
            .ToListAsync();
    }

    /// <summary>Blob eines einzelnen Anhangs (für den Download-Endpoint).</summary>
    public record AttachmentData(byte[] Data, string ContentType, string FileName);

    public async Task<AttachmentData?> GetAttachmentAsync(int protocolId, int fieldId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ProtocolFieldAttachments
            .Where(a => a.ProtocolId == protocolId && a.FormFieldId == fieldId)
            .Select(a => new AttachmentData(a.Data, a.ContentType, a.FileName))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Löscht ein einzelnes Prüfprotokoll. Nur für Administratoren zulässig – das Recht wird
    /// serverseitig anhand des aktuellen Benutzers geprüft (dem Client wird nicht vertraut).
    /// Die erfassten Feldwerte werden per Cascade mitgelöscht. Gibt bei Blockade eine
    /// Fehlermeldung zurück, sonst null.
    /// </summary>
    public async Task<string?> DeleteAsync(int id, int? currentUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var isAdmin = currentUserId is int uid
            && await db.Users.AnyAsync(u => u.Id == uid && u.IsActive && u.IsAdmin);
        if (!isAdmin)
        {
            return "Nur Administratoren dürfen Prüfprotokolle löschen.";
        }

        var protocol = await db.InspectionProtocols.FindAsync(id);
        if (protocol is null)
        {
            return null; // bereits gelöscht – nichts zu tun
        }

        db.InspectionProtocols.Remove(protocol);
        await db.SaveChangesAsync();
        return null;
    }
}
