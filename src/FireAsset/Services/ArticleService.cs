using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung der Artikelstammdaten inkl. Barcode-Suche und Standortwechsel.
/// </summary>
public class ArticleService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TaskGenerationService _taskGeneration;

    public ArticleService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
    }

    public async Task<List<Article>> GetArticlesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Location)
            .OrderBy(a => a.Identification)
            .ToListAsync();
    }

    public async Task<Article?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Article?> FindByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var value = barcode.Trim();
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles
            .Include(a => a.Category)
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.Barcode == value);
    }

    public async Task<bool> BarcodeExistsAsync(string barcode, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return false;
        var value = barcode.Trim();
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles.AnyAsync(a => a.Barcode == value && a.Id != excludeId);
    }

    /// <summary>
    /// Legt einen Artikel samt initialen Aufgaben in einem atomaren Speichervorgang an.
    /// Gibt Hinweise zur Aufgabenanlage und ggf. eine Fehlermeldung zurück.
    /// </summary>
    public async Task<(string? error, List<string> messages)> CreateAsync(Article article, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Normalize(article);
        article.CreatedAt = DateTime.UtcNow;
        article.CreatedByUserId = userId;
        db.Articles.Add(article);
        var messages = await _taskGeneration.AddInitialTasksAsync(db, article);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Articles.Barcode"))
        {
            return ("Dieser Barcode wird bereits verwendet.", new List<string>());
        }
        return (null, messages);
    }

    /// <summary>
    /// Aktualisiert einen Artikel. Bei Kategoriewechsel werden offene automatische Aufgaben
    /// entfernt und Aufgaben für die neue Kategorie erzeugt (atomar).
    /// Gibt ggf. eine Fehlermeldung sowie Hinweise zur Aufgabenanlage zurück.
    /// </summary>
    public async Task<(string? error, List<string> messages)> UpdateAsync(Article article, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Articles.FindAsync(article.Id);
        if (existing is null) return ("Der Artikel existiert nicht mehr.", new List<string>());

        Normalize(article);
        var categoryChanged = existing.CategoryId != article.CategoryId;

        // Optimistische Nebenläufigkeit: Original-Version stammt aus dem Bearbeitungsdialog.
        db.Entry(existing).Property(a => a.Version).OriginalValue = article.Version;
        existing.Version = article.Version + 1;

        existing.Identification = article.Identification;
        existing.Manufacturer = article.Manufacturer;
        existing.Type = article.Type;
        existing.SerialNumber = article.SerialNumber;
        existing.ManufacturerNumber = article.ManufacturerNumber;
        existing.InventoryNumber = article.InventoryNumber;
        existing.Barcode = article.Barcode;
        existing.AcquisitionDate = article.AcquisitionDate;
        existing.ProductionDate = article.ProductionDate;
        existing.DecommissionDate = article.DecommissionDate;
        existing.LegalBasis = article.LegalBasis;
        existing.EndDate = article.EndDate;
        existing.Description = article.Description;
        existing.CategoryId = article.CategoryId;
        existing.LocationId = article.LocationId;
        existing.IsActive = article.IsActive;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedByUserId = userId;

        var messages = new List<string>();
        if (categoryChanged)
        {
            // Offene automatische Aufgaben gehören zur alten Kategorie und werden ersetzt;
            // manuelle Aufgaben und die erledigte Historie bleiben unangetastet.
            var openAutoTasks = await db.InspectionTasks
                .Where(t => t.ArticleId == existing.Id && !t.IsManual && t.Status != InspectionTaskStatus.Erledigt)
                .ToListAsync();
            db.InspectionTasks.RemoveRange(openAutoTasks);
            messages = await _taskGeneration.AddInitialTasksAsync(db, existing);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (DbErrors.ConcurrencyMessage, new List<string>());
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Articles.Barcode"))
        {
            return ("Dieser Barcode wird bereits verwendet.", new List<string>());
        }
        return (null, messages);
    }

    /// <summary>Löscht einen Artikel. Blockiert, wenn Prüfprotokolle existieren (Historie bleibt erhalten).</summary>
    public async Task<string?> DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.InspectionProtocols.AnyAsync(p => p.ArticleId == id))
        {
            return "Artikel besitzt Prüfprotokolle und kann nicht gelöscht werden (Historie bleibt erhalten). " +
                   "Setzen Sie den Artikel stattdessen auf inaktiv.";
        }

        var article = await db.Articles.FindAsync(id);
        if (article is not null)
        {
            db.Articles.Remove(article); // offene Aufgaben werden per Cascade entfernt
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Restrict-FK: zwischen Prüfung und Löschung ist ein Protokoll entstanden.
                return "Artikel besitzt inzwischen Prüfprotokolle und kann nicht gelöscht werden.";
            }
        }
        return null;
    }

    /// <summary>Standortwechsel per Barcode: Artikel-Barcode + Ziel-Standort-Barcode.</summary>
    public async Task<(bool ok, string message)> ChangeLocationByBarcodeAsync(string? articleBarcode, string? locationBarcode, int? userId)
    {
        if (string.IsNullOrWhiteSpace(articleBarcode) || string.IsNullOrWhiteSpace(locationBarcode))
        {
            return (false, "Bitte beide Barcodes angeben.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Barcode == articleBarcode.Trim());
        if (article is null)
        {
            return (false, $"Kein Artikel mit Barcode „{articleBarcode}“ gefunden.");
        }

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Barcode == locationBarcode.Trim());
        if (location is null)
        {
            return (false, $"Kein Standort mit Barcode „{locationBarcode}“ gefunden.");
        }

        article.LocationId = location.Id;
        article.ModifiedAt = DateTime.UtcNow;
        article.ModifiedByUserId = userId;
        await db.SaveChangesAsync();
        return (true, $"„{article.Identification}“ wurde nach „{location.Name}“ umgelagert.");
    }

    public async Task<List<Category>> GetActiveCategoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }

    private static void Normalize(Article article)
    {
        article.Identification = article.Identification.Trim();
        article.Barcode = string.IsNullOrWhiteSpace(article.Barcode) ? null : article.Barcode.Trim();
        article.InventoryNumber = string.IsNullOrWhiteSpace(article.InventoryNumber) ? null : article.InventoryNumber.Trim();
    }
}
