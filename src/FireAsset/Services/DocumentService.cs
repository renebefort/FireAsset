using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung gespeicherter Dokumente (freier Brief / Verwendungsnachweis). Entwürfe sind frei
/// editierbar; der Abschluss eines Verwendungsnachweises bucht die erfassten Artikel auf den
/// Zielstandort um und legt FTZ-Pool-Geräte still. Abgeschlossene Dokumente sind schreibgeschützt.
/// </summary>
public class DocumentService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TaskGenerationService _taskGeneration;
    private readonly ArticleLogService _articleLog;

    public DocumentService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration, ArticleLogService articleLog)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
        _articleLog = articleLog;
    }

    /// <summary>Zeile für das Dokumente-Grid.</summary>
    public record DocumentRow(
        int Id, DocumentType Type, DocumentStatus Status, string Heading, string? Recipient,
        int ArticleCount, DateTime CreatedAt, string? CreatedByName, DateTime? CompletedAt);

    /// <summary>Ergebnis eines Abschlusses.</summary>
    public record FinalizeResult(string? Error, int MovedCount, int PoolCount, IReadOnlyList<string> Notes);

    public async Task<List<DocumentRow>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentRow(
                d.Id, d.Type, d.Status,
                // Überschrift: Titel, ersatzweise Betreff, sonst "(ohne Titel)".
                d.Title != null && d.Title != "" ? d.Title
                    : (d.Subject != null && d.Subject != "" ? d.Subject : "(ohne Titel)"),
                d.Recipient,
                d.Articles.Count,
                d.CreatedAt,
                d.CreatedByUser != null ? d.CreatedByUser.FirstName + " " + d.CreatedByUser.LastName : null,
                d.CompletedAt))
            .ToListAsync();
    }

    /// <summary>Lädt ein Dokument inkl. Artikelzeilen und Zielstandort für Editor/Ansicht/PDF.</summary>
    public async Task<Document?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Documents
            .AsNoTracking()
            .Include(d => d.Articles.OrderBy(a => a.CategoryNameSnapshot).ThenBy(a => a.BarcodeSnapshot))
                .ThenInclude(a => a.Article)
            .Include(d => d.TargetLocation)
            .Include(d => d.Template)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>Baut einen (noch nicht gespeicherten) Entwurf aus einer Vorlage vor.</summary>
    public async Task<Document?> CreateFromTemplateAsync(int templateId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var t = await db.DocumentTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId);
        if (t is null) return null;

        return new Document
        {
            TemplateId = t.Id,
            Type = t.Type,
            Status = DocumentStatus.Entwurf,
            Title = t.TitleDefault,
            Recipient = t.RecipientDefault,
            Sender = t.SenderDefault,
            Subject = t.SubjectDefault,
            Body = t.BodyDefault,
            Signature = t.SignatureDefault,
            TargetLocationId = t.Type == DocumentType.Verwendungsnachweis ? t.DefaultTargetLocationId : null,
        };
    }

    /// <summary>
    /// Legt einen Entwurf an oder aktualisiert ihn. Abgeschlossene Dokumente sind schreibgeschützt.
    /// Bei Verwendungsnachweisen wird die Artikelliste (Snapshots) vollständig ersetzt.
    /// Gibt (Id, Fehlermeldung) zurück.
    /// </summary>
    public async Task<(int id, string? error)> SaveDraftAsync(Document input, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        Document doc;
        if (input.Id == 0)
        {
            doc = new Document
            {
                TemplateId = input.TemplateId,
                Type = input.Type,
                Status = DocumentStatus.Entwurf,
                CreatedAt = now,
                CreatedByUserId = userId,
                Version = 0,
            };
            db.Documents.Add(doc);
        }
        else
        {
            doc = await db.Documents.Include(d => d.Articles).FirstOrDefaultAsync(d => d.Id == input.Id)
                  ?? throw new InvalidOperationException("Dokument nicht gefunden.");
            if (doc.Status == DocumentStatus.Abgeschlossen)
            {
                return (doc.Id, "Das Dokument ist abgeschlossen und kann nicht mehr geändert werden.");
            }
            db.Entry(doc).Property(d => d.Version).OriginalValue = input.Version;
            doc.Version = input.Version + 1;
            doc.ModifiedAt = now;
            doc.ModifiedByUserId = userId;
        }

        ApplyEditableFields(doc, input);

        // Artikelliste nur beim Verwendungsnachweis; bei Aktualisierung komplett ersetzen.
        if (doc.Type == DocumentType.Verwendungsnachweis)
        {
            if (doc.Articles.Count > 0)
            {
                db.DocumentArticles.RemoveRange(doc.Articles);
                doc.Articles.Clear();
            }
            foreach (var a in input.Articles)
            {
                doc.Articles.Add(new DocumentArticle
                {
                    ArticleId = a.ArticleId,
                    BarcodeSnapshot = a.BarcodeSnapshot,
                    IdentificationSnapshot = a.IdentificationSnapshot,
                    CategoryNameSnapshot = a.CategoryNameSnapshot,
                });
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (doc.Id, DbErrors.ConcurrencyMessage);
        }
        return (doc.Id, null);
    }

    /// <summary>Löscht einen Entwurf. Abgeschlossene Dokumente werden nicht gelöscht.</summary>
    public async Task<string?> DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return null;
        if (doc.Status == DocumentStatus.Abgeschlossen)
        {
            return "Abgeschlossene Dokumente können nicht gelöscht werden.";
        }
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Schließt ein Dokument ab (schreibgeschützt). Bei Verwendungsnachweisen laufen in einer
    /// Transaktion: Umbuchung aller erfassten Artikel auf den Zielstandort und Stilllegung der
    /// FTZ-Pool-Geräte (offene Aufgaben → Stillgelegt, Artikel inaktiv, Ende-Datum = heute).
    /// </summary>
    public async Task<FinalizeResult> FinalizeAsync(int id, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var doc = await db.Documents.Include(d => d.Articles).FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return new FinalizeResult("Das Dokument existiert nicht mehr.", 0, 0, []);
        if (doc.Status == DocumentStatus.Abgeschlossen)
        {
            return new FinalizeResult("Das Dokument ist bereits abgeschlossen.", 0, 0, []);
        }

        var notes = new List<string>();
        var movedCount = 0;
        var poolCount = 0;

        if (doc.Type == DocumentType.Verwendungsnachweis)
        {
            if (doc.TargetLocationId is not int targetId)
            {
                return new FinalizeResult("Bitte zuerst einen Zielstandort wählen.", 0, 0, []);
            }
            if (!await db.Locations.AnyAsync(l => l.Id == targetId))
            {
                return new FinalizeResult("Der Zielstandort existiert nicht mehr.", 0, 0, []);
            }
            if (doc.Articles.Count == 0)
            {
                return new FinalizeResult("Es sind keine Artikel erfasst.", 0, 0, []);
            }

            var articleIds = doc.Articles
                .Where(a => a.ArticleId is not null)
                .Select(a => a.ArticleId!.Value)
                .Distinct()
                .ToList();
            var articles = await db.Articles.Where(a => articleIds.Contains(a.Id)).ToListAsync();
            var today = DateTime.Today;
            var now = DateTime.UtcNow;
            // Standortnamen für das Logbuch einmalig auflösen.
            var locationNames = await db.Locations.ToDictionaryAsync(l => l.Id, l => l.Name);
            var targetName = locationNames.GetValueOrDefault(targetId);
            var userName = await _articleLog.ResolveUserNameAsync(db, userId);

            foreach (var article in articles)
            {
                var fromName = article.LocationId is int o ? locationNames.GetValueOrDefault(o) : null;
                article.LocationId = targetId;
                article.ModifiedAt = now;
                article.ModifiedByUserId = userId;
                _articleLog.Add(db, article, ArticleLogAction.Standortwechsel,
                    ArticleLogService.LocationChange(fromName, targetName), userId, userName);
                movedCount++;

                if (article.IsPoolDevice)
                {
                    // Offene Aufgaben stilllegen (nicht löschen – Historie bleibt erhalten).
                    await db.InspectionTasks
                        .Where(t => t.ArticleId == article.Id
                                    && t.Status != InspectionTaskStatus.Erledigt
                                    && t.Status != InspectionTaskStatus.Stillgelegt)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, InspectionTaskStatus.Stillgelegt));

                    // Stilllegung (inkl. Logbuch-Eintrag) über die zentrale Methode.
                    var note = await _taskGeneration.FinalizePoolDeviceAsync(db, article, today, userId);
                    if (note is not null) notes.Add(note);
                    poolCount++;
                }
            }
        }

        doc.Status = DocumentStatus.Abgeschlossen;
        doc.CompletedAt = DateTime.UtcNow;
        doc.CompletedByUserId = userId;
        db.Entry(doc).Property(d => d.Version).OriginalValue = doc.Version;
        doc.Version += 1;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new FinalizeResult(DbErrors.ConcurrencyMessage, 0, 0, []);
        }
        await tx.CommitAsync();
        return new FinalizeResult(null, movedCount, poolCount, notes);
    }

    private static void ApplyEditableFields(Document doc, Document input)
    {
        doc.Title = Clean(input.Title);
        doc.Recipient = Clean(input.Recipient);
        doc.Sender = Clean(input.Sender);
        doc.Signature = Clean(input.Signature);

        if (doc.Type == DocumentType.Brief)
        {
            doc.Subject = Clean(input.Subject);
            doc.Body = Clean(input.Body);
        }
        else
        {
            doc.UsageKind = input.UsageKind;
            doc.UsagePurpose = Clean(input.UsagePurpose);
            doc.UsageDate = input.UsageDate;
            doc.OrderDate = input.OrderDate;
            doc.TargetLocationId = input.TargetLocationId;
            doc.Remarks = Clean(input.Remarks);
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
