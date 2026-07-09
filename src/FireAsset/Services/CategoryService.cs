using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung von Kategorien und deren Prüfintervallen.
/// </summary>
public class CategoryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CategoryService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    // --- Kategorien ---

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Categories
            .Include(c => c.Intervals)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task CreateCategoryAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Categories.Add(category);
        await db.SaveChangesAsync();
    }

    public async Task UpdateCategoryAsync(Category category)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Categories.FindAsync(category.Id);
        if (existing is null) return;

        existing.Name = category.Name;
        existing.Description = category.Description;
        existing.IsActive = category.IsActive;
        await db.SaveChangesAsync();
    }

    public async Task<string?> DeleteCategoryAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Articles.AnyAsync(a => a.CategoryId == id))
        {
            return "Kategorie ist Artikeln zugeordnet und kann nicht gelöscht werden.";
        }

        var category = await db.Categories.FindAsync(id);
        if (category is not null)
        {
            // Intervalle werden per Cascade mit entfernt.
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        }
        return null;
    }

    // --- Intervalle ---

    public async Task<List<InspectionInterval>> GetIntervalsAsync(int categoryId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionIntervals
            .Include(i => i.Form)
            .Where(i => i.CategoryId == categoryId)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<InspectionInterval?> GetIntervalAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.InspectionIntervals.FindAsync(id);
    }

    public async Task CreateIntervalAsync(InspectionInterval interval)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.InspectionIntervals.Add(interval);
        await db.SaveChangesAsync();
    }

    public async Task UpdateIntervalAsync(InspectionInterval interval)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.InspectionIntervals.FindAsync(interval.Id);
        if (existing is null) return;

        existing.Name = interval.Name;
        existing.IntervalMonths = interval.IntervalMonths;
        existing.FormId = interval.FormId;
        existing.IsActive = interval.IsActive;
        await db.SaveChangesAsync();
    }

    public async Task DeleteIntervalAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var interval = await db.InspectionIntervals.FindAsync(id);
        if (interval is not null)
        {
            db.InspectionIntervals.Remove(interval);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Formulare für die Auswahl im Intervall (leer, bis Formulare in M4 gepflegt werden).</summary>
    public async Task<List<Form>> GetFormsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Forms
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }
}
