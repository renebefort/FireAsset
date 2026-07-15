using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung von Kategorien und deren Prüfintervallen.
/// </summary>
public class CategoryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TaskGenerationService _taskGeneration;

    public CategoryService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
    }

    // --- Kategorien ---

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories
            .Include(c => c.Intervals)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>Legt eine Kategorie an. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.</summary>
    public async Task<string?> CreateCategoryAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        category.Name = category.Name.Trim();
        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Categories.Name"))
        {
            return "Eine Kategorie mit diesem Namen existiert bereits.";
        }
        return null;
    }

    /// <summary>Aktualisiert eine Kategorie. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.</summary>
    public async Task<string?> UpdateCategoryAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Categories.FindAsync(category.Id);
        if (existing is null) return "Die Kategorie existiert nicht mehr.";

        db.Entry(existing).Property(c => c.Version).OriginalValue = category.Version;
        existing.Version = category.Version + 1;

        existing.Name = category.Name.Trim();
        existing.Description = category.Description;
        existing.IsActive = category.IsActive;
        existing.ContactUserId = category.ContactUserId;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return DbErrors.ConcurrencyMessage;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Categories.Name"))
        {
            return "Eine Kategorie mit diesem Namen existiert bereits.";
        }
        return null;
    }

    public async Task<string?> DeleteCategoryAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Articles.AnyAsync(a => a.CategoryId == id))
        {
            return "Kategorie ist Artikeln zugeordnet und kann nicht gelöscht werden.";
        }

        var category = await db.Categories.FindAsync(id);
        if (category is not null)
        {
            // Intervalle werden per Cascade mit entfernt.
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        }
        return null;
    }

    // --- Intervalle ---

    public async Task<List<InspectionInterval>> GetIntervalsAsync(int categoryId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionIntervals
            .Include(i => i.Form)
            .Where(i => i.CategoryId == categoryId)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<InspectionInterval?> GetIntervalAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionIntervals.FindAsync(id);
    }

    /// <summary>
    /// Legt ein Intervall an und erzeugt (atomar) Aufgaben für bereits vorhandene aktive Artikel
    /// der Kategorie. Gibt die Anzahl erzeugter Aufgaben zurück.
    /// </summary>
    public async Task<(string? error, int createdTasks)> CreateIntervalAsync(InspectionInterval interval)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        interval.Name = interval.Name.Trim();
        db.InspectionIntervals.Add(interval);
        await db.SaveChangesAsync();

        var created = await _taskGeneration.AddMissingTasksForIntervalAsync(db, interval);
        if (created > 0)
        {
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return (null, created);
    }

    /// <summary>
    /// Aktualisiert ein Intervall. Wird es dabei nutzbar (aktiv + Formular), werden für Artikel
    /// ohne offene Aufgabe dieses Intervalls Aufgaben nachgezogen.
    /// </summary>
    public async Task<(string? error, int createdTasks)> UpdateIntervalAsync(InspectionInterval interval)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.InspectionIntervals.FindAsync(interval.Id);
        if (existing is null) return ("Das Intervall existiert nicht mehr.", 0);

        var wasUsable = existing.IsActive && existing.FormId is not null;

        db.Entry(existing).Property(i => i.Version).OriginalValue = interval.Version;
        existing.Version = interval.Version + 1;

        existing.Name = interval.Name.Trim();
        existing.IntervalMonths = interval.IntervalMonths;
        existing.FormId = interval.FormId;
        existing.IsActive = interval.IsActive;
        existing.IsEntryControl = interval.IsEntryControl;

        var created = 0;
        if (!wasUsable && existing.IsActive && existing.FormId is not null)
        {
            created = await _taskGeneration.AddMissingTasksForIntervalAsync(db, existing);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (DbErrors.ConcurrencyMessage, 0);
        }
        return (null, created);
    }

    public async Task DeleteIntervalAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var interval = await db.InspectionIntervals.FindAsync(id);
        if (interval is not null)
        {
            db.InspectionIntervals.Remove(interval);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Auswählbare Ansprechpartner: aktive Benutzer, plus der ggf. bereits gesetzte (auch wenn
    /// inaktiv), damit eine bestehende Zuordnung im Dropdown sichtbar bleibt.
    /// </summary>
    public async Task<List<User>> GetSelectableContactsAsync(int? includeUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users
            .Where(u => u.IsActive || (includeUserId != null && u.Id == includeUserId))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    /// <summary>Formulare für die Auswahl im Intervall (leer, bis Formulare in M4 gepflegt werden).</summary>
    public async Task<List<Form>> GetFormsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Forms
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }
}
