using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung von Formularen inkl. Versionierung. Strukturänderungen (Felder) erzeugen eine
/// neue Version; bereits von Protokollen referenzierte Versionen bleiben unverändert.
/// Reine Metadaten (Name, Beschreibung, Aktiv) ändern die Version nicht.
/// </summary>
public class FormService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public FormService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Zusammenfassung eines Formulars für die Übersicht.</summary>
    public record FormSummary(int Id, string Name, string? Description, bool IsActive,
        DateTime CreatedAt, int? CurrentVersionId, int CurrentVersionNumber, int VersionCount, int FieldCount);

    public async Task<List<FormSummary>> GetFormsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Forms
            .OrderBy(f => f.Name)
            .Select(f => new FormSummary(
                f.Id,
                f.Name,
                f.Description,
                f.IsActive,
                f.CreatedAt,
                f.CurrentVersionId,
                f.CurrentVersion != null ? f.CurrentVersion.VersionNumber : 0,
                f.Versions.Count,
                f.CurrentVersion != null ? f.CurrentVersion.Fields.Count : 0))
            .ToListAsync();
    }

    /// <summary>Lädt ein Formular als Bearbeitungsmodell (Felder aus der aktuellen Version).</summary>
    public async Task<FormEditModel?> GetEditModelAsync(int formId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var form = await db.Forms
            .Include(f => f.CurrentVersion!).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(f => f.Id == formId);
        if (form is null) return null;

        return new FormEditModel
        {
            FormId = form.Id,
            Name = form.Name,
            Description = form.Description,
            IsActive = form.IsActive,
            Version = form.Version,
            Fields = form.CurrentVersion is null
                ? new()
                : form.CurrentVersion.Fields.OrderBy(f => f.SortOrder)
                    .Select(FormFieldModel.FromEntity).ToList(),
        };
    }

    public async Task<List<FormVersion>> GetVersionsAsync(int formId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.FormVersions
            .Include(v => v.Fields)
            .Include(v => v.EditedByUser)
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Speichert das Bearbeitungsmodell (atomar). Neu = Formular + Version 1. Bestehend = Metadaten
    /// aktualisieren und bei geänderter Feldstruktur eine neue Version anlegen.
    /// Gibt bei Blockade eine Fehlermeldung zurück, sonst null.
    /// </summary>
    public async Task<string?> SaveAsync(FormEditModel model, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        if (model.FormId is null)
        {
            var form = new Form
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow,
            };
            db.Forms.Add(form);
            await db.SaveChangesAsync();

            var version = BuildVersion(form.Id, 1, model.Fields, userId);
            db.FormVersions.Add(version);
            await db.SaveChangesAsync();

            form.CurrentVersionId = version.Id;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        var existing = await db.Forms
            .Include(f => f.CurrentVersion!).ThenInclude(v => v.Fields)
            .FirstOrDefaultAsync(f => f.Id == model.FormId);
        if (existing is null) return "Das Formular existiert nicht mehr.";

        db.Entry(existing).Property(f => f.Version).OriginalValue = model.Version;
        existing.Version = model.Version + 1;

        existing.Name = model.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description;
        existing.IsActive = model.IsActive;

        try
        {
            if (FieldsChanged(existing.CurrentVersion, model.Fields))
            {
                var nextNumber = await db.FormVersions
                    .Where(v => v.FormId == existing.Id)
                    .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
                var version = BuildVersion(existing.Id, nextNumber + 1, model.Fields, userId);
                db.FormVersions.Add(version);
                await db.SaveChangesAsync();

                existing.CurrentVersionId = version.Id;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return DbErrors.ConcurrencyMessage;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "FormVersions"))
        {
            // Zwei Bearbeiter haben zeitgleich dieselbe Versionsnummer berechnet.
            return DbErrors.ConcurrencyMessage;
        }
        return null;
    }

    public async Task<string?> DeleteAsync(int formId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.InspectionIntervals.AnyAsync(i => i.FormId == formId))
        {
            return "Formular ist einem Prüfintervall zugeordnet und kann nicht gelöscht werden.";
        }
        if (await db.InspectionTasks.AnyAsync(t => t.FormId == formId))
        {
            return "Formular wird von Prüfaufgaben verwendet und kann nicht gelöscht werden.";
        }
        if (await db.InspectionProtocols.AnyAsync(p => p.FormVersion.FormId == formId))
        {
            return "Formular wird von Prüfprotokollen referenziert und kann nicht gelöscht werden.";
        }

        var form = await db.Forms.FindAsync(formId);
        if (form is not null)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            // Aktuelle-Version-Verweis lösen, dann Versionen (Cascade) + Formular entfernen.
            form.CurrentVersionId = null;
            await db.SaveChangesAsync();
            db.Forms.Remove(form);
            try
            {
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Restrict-FK: zwischen Prüfung und Löschung ist eine Referenz entstanden.
                return "Formular wird inzwischen referenziert und kann nicht gelöscht werden.";
            }
        }
        return null;
    }

    private static FormVersion BuildVersion(int formId, int number, List<FormFieldModel> fields, int? userId)
    {
        return new FormVersion
        {
            FormId = formId,
            VersionNumber = number,
            CreatedAt = DateTime.UtcNow,
            EditedByUserId = userId,
            Fields = fields.Select((f, index) => new FormField
            {
                Label = f.Label.Trim(),
                FieldType = f.FieldType,
                SortOrder = index,
                ReferenceValue = string.IsNullOrWhiteSpace(f.ReferenceValue) ? null : f.ReferenceValue.Trim(),
                Unit = string.IsNullOrWhiteSpace(f.Unit) ? null : f.Unit.Trim(),
                ShowLastValue = f.ShowLastValue,
            }).ToList(),
        };
    }

    private static bool FieldsChanged(FormVersion? current, List<FormFieldModel> fields)
    {
        if (current is null) return true;
        var currentFields = current.Fields.OrderBy(f => f.SortOrder).ToList();
        if (currentFields.Count != fields.Count) return true;

        for (var i = 0; i < fields.Count; i++)
        {
            var a = currentFields[i];
            var b = fields[i];
            if (a.Label.Trim() != b.Label.Trim()) return true;
            if (a.FieldType != b.FieldType) return true;
            if ((a.ReferenceValue ?? "") != (b.ReferenceValue?.Trim() ?? "")) return true;
            if ((a.Unit ?? "") != (b.Unit?.Trim() ?? "")) return true;
            if (a.ShowLastValue != b.ShowLastValue) return true;
        }
        return false;
    }
}
