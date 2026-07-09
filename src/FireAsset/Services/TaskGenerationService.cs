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
///
/// Alle Methoden arbeiten auf einem vom Aufrufer verwalteten <see cref="AppDbContext"/> und
/// speichern selbst nicht – so bleiben Artikel-/Prüfungs-Schreibvorgänge atomar.
/// </summary>
public class TaskGenerationService
{
    /// <summary>
    /// Fügt für alle aktiven Intervalle der Kategorie des Artikels Aufgaben hinzu (ohne Save).
    /// Gibt Meldungen zu übersprungenen Intervallen zurück.
    /// </summary>
    public async Task<List<string>> AddInitialTasksAsync(AppDbContext db, Article article)
    {
        var messages = new List<string>();
        var intervals = await db.InspectionIntervals
            .Where(i => i.CategoryId == article.CategoryId && i.IsActive)
            .ToListAsync();

        foreach (var interval in intervals)
        {
            var message = AddTask(db, article, interval);
            if (message is not null)
            {
                messages.Add(message);
            }
        }
        return messages;
    }

    /// <summary>
    /// Fügt nach Abschluss einer intervallbasierten Aufgabe die Folgeaufgabe hinzu (ohne Save).
    /// <paramref name="task"/> muss mit geladenem Intervall und Artikel übergeben werden.
    /// Gibt eine Meldung zurück, falls keine Folgeaufgabe erstellt wurde.
    /// </summary>
    public string? AddFollowUpTask(AppDbContext db, InspectionTask task, DateTime completedDate)
    {
        if (task.Interval is null)
        {
            return null; // manuelle Aufgabe – keine Folge
        }
        if (!task.Interval.IsActive || task.Interval.FormId is null)
        {
            return $"Keine Folgeaufgabe erstellt: Das Intervall „{task.Interval.Name}“ ist deaktiviert " +
                   "oder hat kein Formular – die Prüfkette endet hier.";
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
            Status = InspectionTaskStatus.Neu,
            IsManual = false,
            CreatedAt = DateTime.UtcNow,
        });
        return null;
    }

    /// <summary>
    /// Fügt für alle aktiven Artikel der Kategorie eines Intervalls fehlende Aufgaben hinzu (ohne Save),
    /// z. B. nachdem ein Intervall neu angelegt oder (wieder) mit Formular aktiviert wurde.
    /// Gibt die Anzahl der erzeugten Aufgaben zurück.
    /// </summary>
    public async Task<int> AddMissingTasksForIntervalAsync(AppDbContext db, InspectionInterval interval)
    {
        if (!interval.IsActive || interval.FormId is null)
        {
            return 0;
        }

        var articles = await db.Articles
            .Where(a => a.CategoryId == interval.CategoryId && a.IsActive)
            .ToListAsync();
        var articleIdsWithOpenTask = await db.InspectionTasks
            .Where(t => t.IntervalId == interval.Id && t.Status != InspectionTaskStatus.Erledigt)
            .Select(t => t.ArticleId)
            .Distinct()
            .ToListAsync();

        var created = 0;
        foreach (var article in articles.Where(a => !articleIdsWithOpenTask.Contains(a.Id)))
        {
            if (AddTask(db, article, interval) is null)
            {
                created++;
            }
        }
        return created;
    }

    /// <summary>Fügt eine einzelne Aufgabe hinzu; gibt bei Nichtanlage die Begründung zurück.</summary>
    private static string? AddTask(AppDbContext db, Article article, InspectionInterval interval)
    {
        if (interval.FormId is null)
        {
            return $"Intervall „{interval.Name}“ übersprungen: kein Formular hinterlegt.";
        }

        var dueDate = article.AcquisitionDate.AddMonths(interval.IntervalMonths);
        if (article.EndDate is DateTime end && dueDate.Date > end.Date)
        {
            return $"Keine Aufgabe für „{interval.Name}“ erstellt: Fälligkeitsdatum " +
                   $"({dueDate:dd.MM.yyyy}) liegt nach dem Ende-Datum des Artikels ({end:dd.MM.yyyy}).";
        }

        db.InspectionTasks.Add(new InspectionTask
        {
            // Navigation statt ArticleId: funktioniert auch für noch nicht gespeicherte Artikel.
            Article = article,
            IntervalId = interval.Id,
            FormId = interval.FormId.Value,
            DueDate = dueDate,
            Status = InspectionTaskStatus.Neu,
            IsManual = false,
            CreatedAt = DateTime.UtcNow,
        });
        return null;
    }
}
