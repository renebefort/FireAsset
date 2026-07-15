using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung der Dokumentvorlagen (Stammdaten). Vorlagen belegen beim Anlegen eines Dokuments
/// die editierbaren Abschnitte vor.
/// </summary>
public class DocumentTemplateService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DocumentTemplateService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Alle Vorlagen inkl. Default-Zielstandort (für das Stammdaten-Grid).</summary>
    public async Task<List<DocumentTemplate>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.DefaultTargetLocation)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>Aktive Vorlagen (für das Dropdown "Neues Dokument"), alphabetisch.</summary>
    public async Task<List<DocumentTemplate>> GetActiveAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DocumentTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<DocumentTemplate?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DocumentTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<string?> CreateAsync(DocumentTemplate template)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Normalize(template);
        db.DocumentTemplates.Add(template);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return "Es existiert bereits eine Vorlage mit diesem Namen.";
        }
        return null;
    }

    public async Task<string?> UpdateAsync(DocumentTemplate template)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.DocumentTemplates.FindAsync(template.Id);
        if (existing is null) return "Die Vorlage existiert nicht mehr.";

        Normalize(template);
        db.Entry(existing).Property(t => t.Version).OriginalValue = template.Version;
        existing.Version = template.Version + 1;

        existing.Name = template.Name;
        existing.Type = template.Type;
        existing.IsActive = template.IsActive;
        existing.TitleDefault = template.TitleDefault;
        existing.RecipientDefault = template.RecipientDefault;
        existing.SenderDefault = template.SenderDefault;
        existing.SubjectDefault = template.SubjectDefault;
        existing.BodyDefault = template.BodyDefault;
        existing.SignatureDefault = template.SignatureDefault;
        existing.DefaultTargetLocationId = template.DefaultTargetLocationId;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return DbErrors.ConcurrencyMessage;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return "Es existiert bereits eine Vorlage mit diesem Namen.";
        }
        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var template = await db.DocumentTemplates.FindAsync(id);
        if (template is not null)
        {
            db.DocumentTemplates.Remove(template);
            await db.SaveChangesAsync();
        }
        return null;
    }

    private static void Normalize(DocumentTemplate t)
    {
        t.Name = t.Name.Trim();
        t.TitleDefault = Clean(t.TitleDefault);
        t.RecipientDefault = Clean(t.RecipientDefault);
        t.SenderDefault = Clean(t.SenderDefault);
        t.SubjectDefault = Clean(t.SubjectDefault);
        t.BodyDefault = Clean(t.BodyDefault);
        t.SignatureDefault = Clean(t.SignatureDefault);
        // Default-Zielstandort ist nur beim Verwendungsnachweis sinnvoll.
        if (t.Type != DocumentType.Verwendungsnachweis)
        {
            t.DefaultTargetLocationId = null;
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
