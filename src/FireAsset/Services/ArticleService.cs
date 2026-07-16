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
    private readonly ArticleLogService _articleLog;

    public ArticleService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration, ArticleLogService articleLog)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
        _articleLog = articleLog;
    }

    public async Task<List<Article>> GetArticlesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles
            .Include(a => a.Category).ThenInclude(c => c.ContactUser)
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
        await _articleLog.LogAsync(db, article, ArticleLogAction.Angelegt, null, userId);

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
        // Vorzustand für das Logbuch festhalten, bevor die Felder überschrieben werden.
        var oldLocationId = existing.LocationId;
        var wasActive = existing.IsActive;

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
        existing.PurchasePrice = article.PurchasePrice;
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
            // Kein Nachziehen der Eingangskontrolle beim Kategoriewechsel – die entsteht nur bei Neuanlage.
            messages = await _taskGeneration.AddInitialTasksAsync(db, existing, includeEntryControl: false);
        }

        // Logbuch: Standortwechsel und/oder Stilllegung getrennt erfassen; sonst generisch "Editiert".
        var locationChanged = oldLocationId != article.LocationId;
        var deactivated = wasActive && !article.IsActive;
        var userName = await _articleLog.ResolveUserNameAsync(db, userId);
        if (locationChanged)
        {
            var ids = new List<int>();
            if (oldLocationId is int o) ids.Add(o);
            if (article.LocationId is int n) ids.Add(n);
            var names = await db.Locations.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
            var from = oldLocationId is int oo ? names.GetValueOrDefault(oo) : null;
            var to = article.LocationId is int nn ? names.GetValueOrDefault(nn) : null;
            _articleLog.Add(db, existing, ArticleLogAction.Standortwechsel, ArticleLogService.LocationChange(from, to), userId, userName);
        }
        if (deactivated)
        {
            _articleLog.Add(db, existing, ArticleLogAction.InaktivGesetzt, null, userId, userName);
        }
        if (!locationChanged && !deactivated)
        {
            _articleLog.Add(db, existing, ArticleLogAction.Editiert, null, userId, userName);
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

        var fromName = article.LocationId is int oldId
            ? await db.Locations.Where(l => l.Id == oldId).Select(l => l.Name).FirstOrDefaultAsync()
            : null;
        article.LocationId = location.Id;
        article.ModifiedAt = DateTime.UtcNow;
        article.ModifiedByUserId = userId;
        await _articleLog.LogAsync(db, article, ArticleLogAction.Standortwechsel,
            ArticleLogService.LocationChange(fromName, location.Name), userId);
        await db.SaveChangesAsync();
        return (true, $"„{article.Identification}“ wurde nach „{location.Name}“ umgelagert.");
    }

    /// <summary>Barcode + Identifikation aller Artikel mit Barcode – für die Live-Vorschläge der Schnellaktionen.</summary>
    public record BarcodeSuggestion(string Barcode, string Identification);

    public async Task<List<BarcodeSuggestion>> GetBarcodeSuggestionsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles
            .Where(a => a.Barcode != null && a.Barcode != "")
            .OrderBy(a => a.Barcode)
            .Select(a => new BarcodeSuggestion(a.Barcode!, a.Identification))
            .ToListAsync();
    }

    /// <summary>
    /// Bucht mehrere Artikel gemeinsam auf einen Zielstandort (Schnellaktion Stapel-Standortwechsel).
    /// Gibt die Anzahl umgelagerter Artikel und ggf. eine Fehlermeldung zurück.
    /// </summary>
    public async Task<(int moved, string? error)> ChangeLocationForArticlesAsync(
        IReadOnlyList<int> articleIds, int locationId, int? userId)
    {
        if (articleIds.Count == 0) return (0, "Keine Artikel in der Liste.");

        await using var db = await _factory.CreateDbContextAsync();
        var location = await db.Locations.FindAsync(locationId);
        if (location is null) return (0, "Der Zielstandort existiert nicht mehr.");

        var articles = await db.Articles.Where(a => articleIds.Contains(a.Id)).ToListAsync();
        var now = DateTime.UtcNow;
        // Alte Standortnamen für das Logbuch einmalig auflösen.
        var oldIds = articles.Where(a => a.LocationId is not null).Select(a => a.LocationId!.Value).Distinct().ToList();
        var oldNames = await db.Locations.Where(l => oldIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
        var userName = await _articleLog.ResolveUserNameAsync(db, userId);
        foreach (var article in articles)
        {
            var from = article.LocationId is int o ? oldNames.GetValueOrDefault(o) : null;
            article.LocationId = locationId;
            article.ModifiedAt = now;
            article.ModifiedByUserId = userId;
            _articleLog.Add(db, article, ArticleLogAction.Standortwechsel,
                ArticleLogService.LocationChange(from, location.Name), userId, userName);
        }
        await db.SaveChangesAsync();
        return (articles.Count, null);
    }

    public async Task<List<Category>> GetActiveCategoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }

    // --- Artikel-Foto ---------------------------------------------------------------------

    /// <summary>Bilddaten eines Fotos (Detail- oder Thumbnail-Variante) für den Auslieferungs-Endpoint.</summary>
    public record PhotoData(byte[] Data, string ContentType);

    /// <summary>Legt das (bereits verkleinerte) Foto an bzw. ersetzt ein vorhandenes.</summary>
    public async Task SavePhotoAsync(int articleId, ImageProcessing.ProcessedImage processed)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var photo = await db.ArticlePhotos.FirstOrDefaultAsync(p => p.ArticleId == articleId);
        if (photo is null)
        {
            photo = new ArticlePhoto { ArticleId = articleId };
            db.ArticlePhotos.Add(photo);
        }
        photo.ContentType = "image/jpeg";
        photo.Data = processed.Detail;
        photo.Thumbnail = processed.Thumbnail;
        photo.SizeBytes = processed.Detail.LongLength;
        photo.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeletePhotoAsync(int articleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.ArticlePhotos.Where(p => p.ArticleId == articleId).ExecuteDeleteAsync();
    }

    /// <summary>Übernimmt das Foto eines Quell-Artikels auf einen Ziel-Artikel (z. B. beim Kopieren).</summary>
    public async Task CopyPhotoAsync(int sourceArticleId, int targetArticleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var source = await db.ArticlePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.ArticleId == sourceArticleId);
        if (source is null) return;

        var target = await db.ArticlePhotos.FirstOrDefaultAsync(p => p.ArticleId == targetArticleId);
        if (target is null)
        {
            target = new ArticlePhoto { ArticleId = targetArticleId };
            db.ArticlePhotos.Add(target);
        }
        target.ContentType = source.ContentType;
        target.Data = source.Data;
        target.Thumbnail = source.Thumbnail;
        target.SizeBytes = source.SizeBytes;
        target.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Liefert die gewünschte Foto-Variante oder null.</summary>
    public async Task<PhotoData?> GetPhotoAsync(int articleId, bool thumbnail)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.ArticlePhotos
            .Where(p => p.ArticleId == articleId)
            .Select(p => new PhotoData(thumbnail ? p.Thumbnail : p.Data, p.ContentType))
            .FirstOrDefaultAsync();
    }

    /// <summary>Ids aller Artikel, die ein Foto besitzen (für die Grid-Anzeige, ohne Blobs zu laden).</summary>
    public async Task<HashSet<int>> GetPhotoArticleIdsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var ids = await db.ArticlePhotos.Select(p => p.ArticleId).ToListAsync();
        return ids.ToHashSet();
    }

    private static void Normalize(Article article)
    {
        article.Identification = article.Identification.Trim();
        article.Barcode = string.IsNullOrWhiteSpace(article.Barcode) ? null : article.Barcode.Trim();
        article.InventoryNumber = string.IsNullOrWhiteSpace(article.InventoryNumber) ? null : article.InventoryNumber.Trim();
    }
}
