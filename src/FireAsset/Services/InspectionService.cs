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

    /// <summary>Führt eine aufgabenbasierte Prüfung durch. Gibt eine Hinweismeldung zur Folgeaufgabe zurück (oder null).</summary>
    public async Task<string?> ExecuteTaskAsync(int taskId, int formVersionId, Dictionary<int, string?> values,
        InspectionResult result, string? notes, DateTime completedDate, int? userId)
    {
        await using (var db = await _factory.CreateDbContextAsync())
        {
            var task = await db.InspectionTasks
                .Include(t => t.Article)
                .FirstOrDefaultAsync(t => t.Id == taskId);
            if (task is null) return null;

            var protocol = new InspectionProtocol
            {
                ArticleId = task.ArticleId,
                TaskId = task.Id,
                FormVersionId = formVersionId,
                Result = result,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                IsUnplanned = false,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId,
                FieldValues = values.Select(kv => new ProtocolFieldValue { FormFieldId = kv.Key, Value = kv.Value }).ToList(),
            };
            db.InspectionProtocols.Add(protocol);

            task.Status = Data.Entities.TaskStatus.Erledigt;
            task.Article.CurrentInspectionStatus = result;
            await db.SaveChangesAsync();
        }

        return await _taskGeneration.GenerateFollowUpAsync(taskId, completedDate);
    }

    /// <summary>Führt eine ungeplante Prüfung ohne Auswirkung auf Aufgaben durch.</summary>
    public async Task ExecuteUnplannedAsync(int articleId, int formVersionId, Dictionary<int, string?> values,
        InspectionResult result, string? notes, int? userId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var article = await db.Articles.FindAsync(articleId);
        if (article is null) return;

        var protocol = new InspectionProtocol
        {
            ArticleId = articleId,
            TaskId = null,
            FormVersionId = formVersionId,
            Result = result,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            IsUnplanned = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            FieldValues = values.Select(kv => new ProtocolFieldValue { FormFieldId = kv.Key, Value = kv.Value }).ToList(),
        };
        db.InspectionProtocols.Add(protocol);
        article.CurrentInspectionStatus = result;
        await db.SaveChangesAsync();
    }
}
