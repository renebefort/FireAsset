using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>Aggregierte Kennzahlen und fällige Aufgaben für das Dashboard.</summary>
public class DashboardService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DashboardService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public record Stats(
        int ArticlesTotal, int ArticlesActive,
        int TasksOpen, int TasksOverdue, int TasksDueThisMonth,
        int ProtocolsTotal,
        int StatusBestanden, int StatusMangelhaft, int StatusNichtBestanden, int StatusOhne);

    public record DueTask(int TaskId, DateTime DueDate, string ArticleIdentification, string FormName, bool Overdue);

    public async Task<Stats> GetStatsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var today = DateTime.Today;
        var monthEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

        // Deaktivierte Artikel zählen nicht in offene Aufgaben und Statusverteilung
        // (stillgelegte Geräte sollen die Kennzahlen nicht dauerhaft aufblähen).
        var openTasks = db.InspectionTasks
            .Where(t => t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt
                        && t.Article.IsActive);

        return new Stats(
            ArticlesTotal: await db.Articles.CountAsync(),
            ArticlesActive: await db.Articles.CountAsync(a => a.IsActive),
            TasksOpen: await openTasks.CountAsync(),
            TasksOverdue: await openTasks.CountAsync(t => t.DueDate < today),
            TasksDueThisMonth: await openTasks.CountAsync(t => t.DueDate >= today && t.DueDate <= monthEnd),
            ProtocolsTotal: await db.InspectionProtocols.CountAsync(),
            StatusBestanden: await db.Articles.CountAsync(a => a.IsActive && a.CurrentInspectionStatus == InspectionResult.Bestanden),
            StatusMangelhaft: await db.Articles.CountAsync(a => a.IsActive && a.CurrentInspectionStatus == InspectionResult.Mangelhaft),
            StatusNichtBestanden: await db.Articles.CountAsync(a => a.IsActive && a.CurrentInspectionStatus == InspectionResult.NichtBestanden),
            StatusOhne: await db.Articles.CountAsync(a => a.IsActive && a.CurrentInspectionStatus == null));
    }

    public async Task<List<DueTask>> GetUpcomingTasksAsync(int take = 10)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var today = DateTime.Today;
        return await db.InspectionTasks
            .Where(t => t.Status != InspectionTaskStatus.Erledigt
                        && t.Status != InspectionTaskStatus.Stillgelegt
                        && t.Article.IsActive)
            .OrderBy(t => t.DueDate)
            .Take(take)
            .Select(t => new DueTask(t.Id, t.DueDate, t.Article.Identification, t.Form.Name, t.DueDate < today))
            .ToListAsync();
    }
}
