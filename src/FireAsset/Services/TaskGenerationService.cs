using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Erzeugt Prüfaufgaben aus Kategorie-Intervallen. Regeln:
/// - Erste Fälligkeit = Anschaffungsdatum + Rhythmus (Monate).
/// - Folgeaufgabe    = Erledigt-Datum + Rhythmus (Monate).
/// - Liegt die berechnete Fälligkeit nach dem Ende-Datum des Artikels, wird keine Aufgabe angelegt.
/// - Intervalle ohne hinterlegtes Formular werden übersprungen.
/// </summary>
public class TaskGenerationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public TaskGenerationService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Legt beim Anlegen eines Artikels für alle aktiven Intervalle der Kategorie Aufgaben an.
    /// Gibt Meldungen zu übersprungenen Intervallen zurück.
    /// </summary>
    public async Task<List<string>> GenerateInitialTasksAsync(int articleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var messages = new List<string>();

        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == articleId);
        if (article is null) return messages;

        var intervals = await db.InspectionIntervals
            .Where(i => i.CategoryId == article.CategoryId && i.IsActive)
            .ToListAsync();

        foreach (var interval in intervals)
        {
            if (interval.FormId is null)
            {
                messages.Add($"Intervall „{interval.Name}“ übersprungen: kein Formular hinterlegt.");
                continue;
            }

            var dueDate = article.AcquisitionDate.AddMonths(interval.IntervalMonths);
            if (article.EndDate is DateTime end && dueDate.Date > end.Date)
            {
                messages.Add($"Keine Aufgabe für „{interval.Name}“ erstellt: Fälligkeitsdatum " +
                             $"({dueDate:dd.MM.yyyy}) liegt nach dem Ende-Datum des Artikels ({end:dd.MM.yyyy}).");
                continue;
            }

            db.InspectionTasks.Add(new InspectionTask
            {
                ArticleId = article.Id,
                IntervalId = interval.Id,
                FormId = interval.FormId.Value,
                DueDate = dueDate,
                Status = Data.Entities.TaskStatus.Neu,
                IsManual = false,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return messages;
    }

    /// <summary>
    /// Legt nach Abschluss einer intervallbasierten Aufgabe die Folgeaufgabe an.
    /// Gibt eine Meldung zurück, falls keine Folgeaufgabe erstellt wurde (Ende-Datum überschritten).
    /// </summary>
    public async Task<string?> GenerateFollowUpAsync(int completedTaskId, DateTime completedDate)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var task = await db.InspectionTasks
            .Include(t => t.Interval)
            .Include(t => t.Article)
            .FirstOrDefaultAsync(t => t.Id == completedTaskId);

        // Nur für intervallbasierte Aufgaben mit aktivem Intervall.
        if (task?.Interval is null || !task.Interval.IsActive || task.Interval.FormId is null)
        {
            return null;
        }

        var dueDate = completedDate.AddMonths(task.Interval.IntervalMonths);
        if (task.Article.EndDate is DateTime end && dueDate.Date > end.Date)
        {
            return $"Keine Folgeaufgabe erstellt: Fälligkeitsdatum ({dueDate:dd.MM.yyyy}) liegt nach dem " +
                   $"Ende-Datum des Artikels ({end:dd.MM.yyyy}).";
        }

        db.InspectionTasks.Add(new InspectionTask
        {
            ArticleId = task.ArticleId,
            IntervalId = task.IntervalId,
            FormId = task.Interval.FormId.Value,
            DueDate = dueDate,
            Status = Data.Entities.TaskStatus.Neu,
            IsManual = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return null;
    }
}
