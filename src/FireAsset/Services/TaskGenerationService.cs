using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Erzeugt Prüfaufgaben aus Kategorie-Intervallen. Regeln:
/// - Erste Fälligkeit = Anschaffungsdatum + Rhythmus (Monate).
/// - Folgeaufgabe    = Erledigt-Datum + Rhythmus (Monate).
/// - Keine neue Aufgabe, wenn der Artikel inaktiv ist oder die berechnete Fälligkeit nach
///   dem Ende-Datum bzw. dem Ausmusterungsdatum des Artikels liegt.
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
            var message = interval.IsEntryControl
                ? await AddEntryControlTaskAsync(db, article, interval)
                : AddTask(db, article, interval);
            if (message is not null)
            {
                messages.Add(message);
            }
        }
        return messages;
    }

    /// <summary>
    /// Legt für ein Eingangskontroll-Intervall genau einmal eine Aufgabe an (Fälligkeit = heute,
    /// dem Anlagedatum des Artikels). Keine Folgeaufgaben. Gibt bei Nichtanlage die Begründung zurück.
    /// </summary>
    private static async Task<string?> AddEntryControlTaskAsync(AppDbContext db, Article article, InspectionInterval interval)
    {
        if (interval.FormId is null)
        {
            return $"Eingangskontrolle „{interval.Name}“ übersprungen: kein Formular hinterlegt.";
        }
        if (!article.IsActive)
        {
            return $"Keine Eingangskontrolle für „{interval.Name}“ erstellt: Der Artikel ist inaktiv.";
        }
        // Genau einmal: existiert für diesen (bereits gespeicherten) Artikel schon eine Aufgabe
        // dieses Intervalls, wird keine weitere angelegt.
        if (article.Id != 0 &&
            await db.InspectionTasks.AnyAsync(t => t.ArticleId == article.Id && t.IntervalId == interval.Id))
        {
            return null;
        }

        db.InspectionTasks.Add(new InspectionTask
        {
            Article = article,
            IntervalId = interval.Id,
            FormId = interval.FormId.Value,
            DueDate = DateTime.Today,
            Status = InspectionTaskStatus.Neu,
            IsManual = false,
            CreatedAt = DateTime.UtcNow,
        });
        return null;
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
        if (task.Interval.IsEntryControl)
        {
            return null; // Eingangskontrolle ist einmalig – keine Folgeaufgabe
        }
        if (!task.Interval.IsActive || task.Interval.FormId is null)
        {
            return $"Keine Folgeaufgabe erstellt: Das Intervall „{task.Interval.Name}“ ist deaktiviert " +
                   "oder hat kein Formular – die Prüfkette endet hier.";
        }
        if (!task.Article.IsActive)
        {
            return "Keine Folgeaufgabe erstellt: Der Artikel ist inaktiv.";
        }

        var dueDate = completedDate.AddMonths(task.Interval.IntervalMonths);
        if (ExceedsArticleLifetime(task.Article, dueDate, out var limit, out var limitLabel))
        {
            return $"Keine Folgeaufgabe erstellt: Fälligkeitsdatum ({dueDate:dd.MM.yyyy}) liegt nach dem " +
                   $"{limitLabel} des Artikels ({limit:dd.MM.yyyy}).";
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
    /// FTZ-Pool-Gerät: legt bewusst KEINE Folgeaufgabe an. Existiert nach dem Schließen der aktuellen
    /// Aufgabe keine offene Aufgabe mehr, wird der Artikel stillgelegt (inaktiv, Ende-Datum =
    /// Abschlussdatum) und ein Hinweistext zurückgegeben; andernfalls null (es sind noch Aufgaben offen).
    /// Erwartet, dass die aktuelle Aufgabe bereits als erledigt/stillgelegt persistiert wurde (gleiche
    /// Transaktion), damit die Restabfrage den Endzustand widerspiegelt.
    /// </summary>
    public async Task<string?> FinalizePoolDeviceAsync(AppDbContext db, Article article, DateTime closedDate)
    {
        var hasOpenTasks = await db.InspectionTasks.AnyAsync(t =>
            t.ArticleId == article.Id
            && t.Status != InspectionTaskStatus.Erledigt
            && t.Status != InspectionTaskStatus.Stillgelegt);
        if (hasOpenTasks)
        {
            return null;
        }

        article.IsActive = false;
        article.EndDate = closedDate.Date;
        return $"FTZ-Pool-Gerät „{article.Identification}“: letzte Prüfaufgabe abgeschlossen – " +
               $"Artikel wurde automatisch stillgelegt (Ende-Datum {closedDate:dd.MM.yyyy}).";
    }

    /// <summary>
    /// Fügt für alle aktiven Artikel der Kategorie eines Intervalls fehlende Aufgaben hinzu (ohne Save),
    /// z. B. nachdem ein Intervall neu angelegt oder (wieder) mit Formular aktiviert wurde.
    /// Gibt die Anzahl der erzeugten Aufgaben zurück.
    /// </summary>
    public async Task<int> AddMissingTasksForIntervalAsync(AppDbContext db, InspectionInterval interval)
    {
        // Eingangskontrolle entsteht ausschließlich bei der Neuanlage eines Artikels –
        // kein nachträgliches Nachziehen für bestehende Artikel.
        if (interval.IsEntryControl)
        {
            return 0;
        }
        if (!interval.IsActive || interval.FormId is null)
        {
            return 0;
        }

        var articles = await db.Articles
            .Where(a => a.CategoryId == interval.CategoryId && a.IsActive)
            .ToListAsync();
        var articleIdsWithOpenTask = await db.InspectionTasks
            .Where(t => t.IntervalId == interval.Id
                        && t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt)
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
        if (!article.IsActive)
        {
            return $"Keine Aufgabe für „{interval.Name}“ erstellt: Der Artikel ist inaktiv.";
        }

        var dueDate = article.AcquisitionDate.AddMonths(interval.IntervalMonths);
        if (ExceedsArticleLifetime(article, dueDate, out var limit, out var limitLabel))
        {
            return $"Keine Aufgabe für „{interval.Name}“ erstellt: Fälligkeitsdatum " +
                   $"({dueDate:dd.MM.yyyy}) liegt nach dem {limitLabel} des Artikels ({limit:dd.MM.yyyy}).";
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

    /// <summary>
    /// True, wenn die Fälligkeit nach dem Lebensende des Artikels liegt (frühestes Datum aus
    /// Ende-Datum und Ausmusterungsdatum). Liefert das maßgebliche Datum samt Bezeichnung.
    /// </summary>
    private static bool ExceedsArticleLifetime(Article article, DateTime dueDate, out DateTime limit, out string limitLabel)
    {
        limit = default;
        limitLabel = string.Empty;

        if (article.EndDate is DateTime end)
        {
            limit = end.Date;
            limitLabel = "Ende-Datum";
        }
        if (article.DecommissionDate is DateTime decommission &&
            (limitLabel.Length == 0 || decommission.Date < limit))
        {
            limit = decommission.Date;
            limitLabel = "Ausmusterungsdatum";
        }

        return limitLabel.Length > 0 && dueDate.Date > limit;
    }
}
