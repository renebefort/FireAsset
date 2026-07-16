using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Abfrage und Pflege von Prüfaufgaben (Liste, Fälligkeitsänderung, manuelle Anlage).
/// </summary>
public class TaskService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TaskGenerationService _taskGeneration;

    public TaskService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
    }

    /// <summary>Zeile der Aufgabenliste (aus Aufgabe und zugeordnetem Artikel).</summary>
    public record TaskListItem(
        int TaskId,
        DateTime DueDate,
        InspectionTaskStatus Status,
        bool IsManual,
        bool HasInterval,
        bool IsPoolDevice,
        int ArticleId,
        int FormId,
        string ArticleIdentification,
        string? ArticleBarcode,
        string? ArticleInventoryNumber,
        string? CategoryName,
        string? LocationName,
        string? LocationBarcode,
        string IntervalName,
        string FormName,
        DateTime? ArticleEndDate,
        string? ContactName);

    /// <summary>Kurzinfo der direkt zu ladenden Aufgabe (Schnellaktion "Prüfung starten").</summary>
    public record NextTask(int TaskId, int ArticleId, int FormId, string ArticleIdentification, DateTime DueDate);

    /// <summary>
    /// Liefert für einen Artikel die offene Aufgabe mit dem kleinsten Fälligkeitsdatum (oder null,
    /// wenn keine offene Aufgabe existiert). Basis für den Barcode-Schnelleinstieg in die Prüfung.
    /// </summary>
    public async Task<NextTask?> GetNextOpenTaskByArticleAsync(int articleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionTasks
            .Where(t => t.ArticleId == articleId
                        && t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt)
            .OrderBy(t => t.DueDate)
            .Select(t => new NextTask(t.Id, t.ArticleId, t.FormId, t.Article.Identification, t.DueDate))
            .FirstOrDefaultAsync();
    }

    public async Task<List<TaskListItem>> GetTasksAsync(bool includeDone)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.InspectionTasks
            .Include(t => t.Article).ThenInclude(a => a.Category)
            .Include(t => t.Article).ThenInclude(a => a.Location)
            .Include(t => t.Interval)
            .Include(t => t.Form)
            .AsQueryable();

        if (!includeDone)
        {
            query = query.Where(t => t.Status != InspectionTaskStatus.Erledigt
                                     && t.Status != InspectionTaskStatus.Stillgelegt);
        }

        // Offene Aufgaben deaktivierter Artikel ausblenden (erscheinen bei Reaktivierung wieder);
        // die abgeschlossene Historie (erledigt/stillgelegt) bleibt sichtbar.
        query = query.Where(t => t.Article.IsActive
                                 || t.Status == InspectionTaskStatus.Erledigt
                                 || t.Status == InspectionTaskStatus.Stillgelegt);

        return await query
            .OrderBy(t => t.DueDate)
            .Select(t => new TaskListItem(
                t.Id,
                t.DueDate,
                t.Status,
                t.IsManual,
                t.IntervalId != null,
                t.Article.IsPoolDevice,
                t.ArticleId,
                t.FormId,
                t.Article.Identification,
                t.Article.Barcode,
                t.Article.InventoryNumber,
                t.Article.Category != null ? t.Article.Category.Name : null,
                t.Article.Location != null ? t.Article.Location.Name : null,
                t.Article.Location != null ? t.Article.Location.Barcode : null,
                t.Interval != null ? t.Interval.Name : "(manuell)",
                t.Form.Name,
                t.Article.EndDate,
                t.Article.Category != null && t.Article.Category.ContactUser != null
                    ? t.Article.Category.ContactUser.FirstName + " " + t.Article.Category.ContactUser.LastName
                    : null))
            .ToListAsync();
    }

    public async Task UpdateDueDateAsync(int taskId, DateTime dueDate)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.InspectionTasks.FindAsync(taskId);
        if (task is null || task.Status is InspectionTaskStatus.Erledigt or InspectionTaskStatus.Stillgelegt) return;
        task.DueDate = dueDate;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Legt eine offene Aufgabe manuell still (ohne Prüfung/Protokoll). Bei intervallgebundenen
    /// Aufgaben kann optional die Folgeaufgabe erzeugt werden (Basis: heutiges Datum), damit der
    /// Folge-Workflow weiterläuft. Läuft atomar; doppelte Stilllegung wird abgewiesen.
    /// Gibt (Fehlermeldung, Info zur Folgeaufgabe) zurück.
    /// </summary>
    public async Task<(string? error, string? info)> DecommissionAsync(int taskId, bool createFollowUp, int? userId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var claimed = await db.InspectionTasks
            .Where(t => t.Id == taskId
                        && t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, InspectionTaskStatus.Stillgelegt));
        if (claimed == 0)
        {
            return ("Die Aufgabe ist bereits erledigt oder stillgelegt.", null);
        }

        var task = await db.InspectionTasks
            .Include(t => t.Interval)
            .Include(t => t.Article)
            .FirstAsync(t => t.Id == taskId);

        string? info = null;
        if (task.Article.IsPoolDevice)
        {
            // FTZ-Pool-Gerät: keine Folgeaufgabe. War dies die letzte offene Aufgabe, wird der
            // Artikel stillgelegt (Ende-Datum = heute).
            info = await _taskGeneration.FinalizePoolDeviceAsync(db, task.Article, DateTime.Today, userId);
            await db.SaveChangesAsync();
        }
        else if (createFollowUp)
        {
            info = _taskGeneration.AddFollowUpTask(db, task, DateTime.Today)
                   ?? (task.Interval is null ? "Keine Folgeaufgabe: Die Aufgabe ist nicht mit einem Intervall verknüpft." : "Folgeaufgabe wurde angelegt.");
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return (null, info);
    }

    /// <summary>Legt eine manuelle Aufgabe an (ohne Intervallbindung).</summary>
    public async Task CreateManualTaskAsync(int articleId, int formId, DateTime dueDate)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.InspectionTasks.Add(new InspectionTask
        {
            ArticleId = articleId,
            IntervalId = null,
            FormId = formId,
            DueDate = dueDate,
            Status = InspectionTaskStatus.Neu,
            IsManual = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Aktive Formulare für die manuelle Aufgaben-/Prüfungsauswahl.</summary>
    public async Task<List<Form>> GetActiveFormsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Forms.Where(f => f.IsActive && f.CurrentVersionId != null)
            .OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<List<Article>> GetActiveArticlesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Articles.Where(a => a.IsActive).OrderBy(a => a.Identification).ToListAsync();
    }
}
