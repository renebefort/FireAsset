using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Durchführung von Prüfungen: lädt das aktuelle Formular, erzeugt Protokolle,
/// schließt Aufgaben ab und legt Folgeaufgaben an.
/// </summary>
public class InspectionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly TaskGenerationService _taskGeneration;

    public InspectionService(IDbContextFactory<AppDbContext> factory, TaskGenerationService taskGeneration)
    {
        _factory = factory;
        _taskGeneration = taskGeneration;
    }

    /// <summary>Erfassungsmodell: aktuelle Formularversion, Felder und Werte der letzten Prüfung.</summary>
    public record CaptureModel(int FormVersionId, string FormName, List<FormField> Fields,
        Dictionary<string, string?> LastValues);

    /// <summary>Ergebnis einer Prüfungsdurchführung.</summary>
    public record ExecuteResult(bool Ok, string? Error, string? FollowUpInfo)
    {
        public static ExecuteResult Fail(string error) => new(false, error, null);
        public static ExecuteResult Success(string? followUpInfo) => new(true, null, followUpInfo);
    }

    public async Task<CaptureModel?> GetCaptureModelAsync(int formId, int articleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var form = await db.Forms
            .Include(f => f.CurrentVersion!).ThenInclude(v => v.Fields)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formId);

        if (form?.CurrentVersion is null) return null;

        var fields = form.CurrentVersion.Fields.OrderBy(f => f.SortOrder).ToList();
        var lastValues = await GetLastValuesAsync(db, articleId, formId);

        return new CaptureModel(form.CurrentVersion.Id, form.Name, fields, lastValues);
    }

    private static async Task<Dictionary<string, string?>> GetLastValuesAsync(AppDbContext db, int articleId, int formId)
    {
        var lastProtocol = await db.InspectionProtocols
            .Include(p => p.FieldValues).ThenInclude(v => v.FormField)
            .Where(p => p.ArticleId == articleId && p.FormVersion.FormId == formId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        var result = new Dictionary<string, string?>();
        if (lastProtocol is null) return result;

        foreach (var value in lastProtocol.FieldValues)
        {
            // Zuordnung über die Bezeichnung, damit auch versionsübergreifend verglichen werden kann.
            result[value.FormField.Label] = value.Value;
        }
        return result;
    }

    /// <summary>
    /// Führt eine aufgabenbasierte Prüfung durch: schließt die Aufgabe ab, speichert das Protokoll,
    /// aktualisiert optional das Fälligkeitsdatum und legt die Folgeaufgabe an – alles in einer
    /// Transaktion. Doppelte Abschlüsse (Doppelklick, zweiter Bearbeiter) werden abgewiesen.
    /// </summary>
    public async Task<ExecuteResult> ExecuteTaskAsync(int taskId, int formVersionId, Dictionary<int, string?> values,
        InspectionResult result, string? notes, DateTime completedDate, DateTime? newDueDate, int? userId)
    {
        if (completedDate.Date > DateTime.Today)
        {
            return ExecuteResult.Fail("Das Prüfdatum darf nicht in der Zukunft liegen.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Aufgabe atomar "beanspruchen": schlägt fehl, wenn sie bereits erledigt/stillgelegt ist.
        var claimed = await db.InspectionTasks
            .Where(t => t.Id == taskId
                        && t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, InspectionTaskStatus.Erledigt));
        if (claimed == 0)
        {
            return ExecuteResult.Fail("Die Aufgabe wurde bereits abgeschlossen (kein weiteres Protokoll erstellt).");
        }

        var task = await db.InspectionTasks
            .Include(t => t.Article)
            .Include(t => t.Interval)
            .Include(t => t.Form)
            .FirstAsync(t => t.Id == taskId);

        var validationError = await ValidateSubmissionAsync(db, task.Form, formVersionId, values);
        if (validationError is not null)
        {
            return ExecuteResult.Fail(validationError); // Rollback über Dispose der Transaktion
        }

        if (newDueDate is DateTime due)
        {
            task.DueDate = due;
        }

        db.InspectionProtocols.Add(new InspectionProtocol
        {
            ArticleId = task.ArticleId,
            TaskId = task.Id,
            FormVersionId = formVersionId,
            Result = result,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            IsUnplanned = false,
            CompletedDate = completedDate,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedByUserName = await GetUserNameAsync(db, userId),
            FieldValues = values.Select(kv => new ProtocolFieldValue { FormFieldId = kv.Key, Value = kv.Value }).ToList(),
        });
        task.Article.CurrentInspectionStatus = result;

        // FTZ-Pool-Gerät: keine Folgeaufgabe – stattdessen ggf. Artikel stilllegen, wenn dies die letzte
        // offene Aufgabe war. Sonst regulärer wiederkehrender Zyklus mit Folgeaufgabe.
        var followUpInfo = task.Article.IsPoolDevice
            ? await _taskGeneration.FinalizePoolDeviceAsync(db, task.Article, completedDate)
            : _taskGeneration.AddFollowUpTask(db, task, completedDate);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ExecuteResult.Success(followUpInfo);
    }

    /// <summary>Führt eine ungeplante Prüfung ohne Auswirkung auf Aufgaben durch.</summary>
    public async Task<ExecuteResult> ExecuteUnplannedAsync(int articleId, int formVersionId, Dictionary<int, string?> values,
        InspectionResult result, string? notes, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var article = await db.Articles.FindAsync(articleId);
        if (article is null)
        {
            return ExecuteResult.Fail("Der Artikel existiert nicht mehr.");
        }

        var form = await db.Forms.FirstOrDefaultAsync(f => f.CurrentVersionId == formVersionId);
        var validationError = await ValidateSubmissionAsync(db, form, formVersionId, values);
        if (validationError is not null)
        {
            return ExecuteResult.Fail(validationError);
        }

        db.InspectionProtocols.Add(new InspectionProtocol
        {
            ArticleId = articleId,
            TaskId = null,
            FormVersionId = formVersionId,
            Result = result,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            IsUnplanned = true,
            CompletedDate = DateTime.Now,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedByUserName = await GetUserNameAsync(db, userId),
            FieldValues = values.Select(kv => new ProtocolFieldValue { FormFieldId = kv.Key, Value = kv.Value }).ToList(),
        });
        article.CurrentInspectionStatus = result;
        await db.SaveChangesAsync();
        return ExecuteResult.Success(null);
    }

    /// <summary>
    /// Prüft, dass die eingereichte Formularversion noch die aktuelle des Formulars ist und
    /// alle Feld-Ids zu dieser Version gehören (Schutz vor veralteten/manipulierten Dialogen).
    /// </summary>
    private static async Task<string?> ValidateSubmissionAsync(AppDbContext db, Form? form, int formVersionId,
        Dictionary<int, string?> values)
    {
        if (form is null || form.CurrentVersionId != formVersionId)
        {
            return "Das Formular wurde zwischenzeitlich geändert. Bitte den Dialog erneut öffnen.";
        }

        var validFieldIds = await db.FormFields
            .Where(f => f.FormVersionId == formVersionId)
            .Select(f => f.Id)
            .ToListAsync();
        if (values.Keys.Except(validFieldIds).Any())
        {
            return "Die erfassten Werte passen nicht zur aktuellen Formularversion. Bitte den Dialog erneut öffnen.";
        }
        return null;
    }

    private static async Task<string?> GetUserNameAsync(AppDbContext db, int? userId)
    {
        if (userId is null) return null;
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync();
    }
}
