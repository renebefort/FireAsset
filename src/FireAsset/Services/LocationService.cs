using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Verwaltung hierarchischer Standorte.
/// </summary>
public class LocationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LocationService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Location>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Locations
            .Include(l => l.ParentLocation)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    /// <summary>Liefert den vollständigen Pfad eines Standorts (z. B. "Fahrzeug > 46 HLF > Kabine").</summary>
    public static string BuildPath(Location location, IReadOnlyDictionary<int, Location> byId)
    {
        var parts = new List<string>();
        var current = location;
        var guard = 0;
        while (current is not null && guard++ < 50)
        {
            parts.Insert(0, current.Name);
            current = current.ParentLocationId is int pid && byId.TryGetValue(pid, out var parent)
                ? parent
                : null;
        }
        return string.Join(" > ", parts);
    }

    public async Task<bool> BarcodeExistsAsync(string barcode, int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return false;
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Locations.AnyAsync(l => l.Barcode == barcode && l.Id != excludeId);
    }

    public async Task CreateAsync(Location location)
    {
        await using var db = await _factory.CreateDbContextAsync();
        Normalize(location);
        db.Locations.Add(location);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Location location)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Locations.FindAsync(location.Id);
        if (existing is null) return;

        Normalize(location);
        existing.Name = location.Name;
        existing.Description = location.Description;
        existing.Barcode = location.Barcode;
        existing.Icon = location.Icon;
        existing.ParentLocationId = location.ParentLocationId;
        await db.SaveChangesAsync();
    }

    /// <summary>Löscht einen Standort. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.</summary>
    public async Task<string?> DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Locations.AnyAsync(l => l.ParentLocationId == id))
        {
            return "Standort hat untergeordnete Standorte und kann nicht gelöscht werden.";
        }
        if (await db.Articles.AnyAsync(a => a.LocationId == id))
        {
            return "Standort ist Artikeln zugeordnet und kann nicht gelöscht werden.";
        }

        var location = await db.Locations.FindAsync(id);
        if (location is not null)
        {
            db.Locations.Remove(location);
            await db.SaveChangesAsync();
        }
        return null;
    }

    /// <summary>Verhindert, dass ein Standort sich selbst (oder einen Nachfahren) als Elternteil erhält.</summary>
    public async Task<bool> WouldCreateCycleAsync(int locationId, int? newParentId)
    {
        if (newParentId is null) return false;
        if (newParentId == locationId) return true;

        await using var db = await _factory.CreateDbContextAsync();
        var all = await db.Locations.Select(l => new { l.Id, l.ParentLocationId }).ToListAsync();
        var byId = all.ToDictionary(x => x.Id, x => x.ParentLocationId);

        var current = newParentId;
        var guard = 0;
        while (current is int c && guard++ < 100)
        {
            if (c == locationId) return true;
            current = byId.TryGetValue(c, out var p) ? p : null;
        }
        return false;
    }

    private static void Normalize(Location location)
    {
        location.Barcode = string.IsNullOrWhiteSpace(location.Barcode) ? null : location.Barcode.Trim();
        location.Description = string.IsNullOrWhiteSpace(location.Description) ? null : location.Description;
        location.Icon = string.IsNullOrWhiteSpace(location.Icon) ? null : location.Icon;
    }
}
