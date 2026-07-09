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

    public TaskService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Zeile der Aufgabenliste (aus Aufgabe und zugeordnetem Artikel).</summary>
    public record TaskListItem(
        int TaskId,
        DateTime DueDate,
        InspectionTaskStatus Status,
        bool IsManual,
        int ArticleId,
        int FormId,
        string ArticleIdentification,
        string? ArticleBarcode,
        string? ArticleInventoryNumber,
        string? CategoryName,
        string? LocationName,
        string IntervalName,
        string FormName,
        DateTime? ArticleEndDate);

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
            query = query.Where(t => t.Status != InspectionTaskStatus.Erledigt);
        }

        // Offene Aufgaben deaktivierter Artikel ausblenden (erscheinen bei Reaktivierung wieder);
        // die erledigte Historie bleibt sichtbar.
        query = query.Where(t => t.Article.IsActive || t.Status == InspectionTaskStatus.Erledigt);

        return await query
            .OrderBy(t => t.DueDate)
            .Select(t => new TaskListItem(
                t.Id,
                t.DueDate,
                t.Status,
                t.IsManual,
                t.ArticleId,
                t.FormId,
                t.Article.Identification,
                t.Article.Barcode,
                t.Article.InventoryNumber,
                t.Article.Category != null ? t.Article.Category.Name : null,
                t.Article.Location != null ? t.Article.Location.Name : null,
                t.Interval != null ? t.Interval.Name : "(manuell)",
                t.Form.Name,
                t.Article.EndDate))
            .ToListAsync();
    }

    public async Task UpdateDueDateAsync(int taskId, DateTime dueDate)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.InspectionTasks.FindAsync(taskId);
        if (task is null || task.Status == InspectionTaskStatus.Erledigt) return;
        task.DueDate = dueDate;
        await db.SaveChangesAsync();
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
