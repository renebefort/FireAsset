using System.Globalization;
using System.Text;
using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>Erzeugt CSV-Exporte (Inventarliste), filterbar nach Kategorie, Standort und Status.</summary>
public class ExportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ExportService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Baut die Inventarliste als CSV (Semikolon-getrennt, UTF-8 mit BOM für Excel).</summary>
    public async Task<byte[]> BuildInventoryCsvAsync(int? categoryId, int? locationId, bool? isActive)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Articles
            .Include(a => a.Category)
            .Include(a => a.Location)
            .AsQueryable();

        if (categoryId is int c) query = query.Where(a => a.CategoryId == c);
        if (locationId is int l) query = query.Where(a => a.LocationId == l);
        if (isActive is bool active) query = query.Where(a => a.IsActive == active);

        var articles = await query.OrderBy(a => a.Identification).ToListAsync();

        var sb = new StringBuilder();
        var header = new[]
        {
            "Identifikation", "Hersteller", "Typ", "Seriennummer", "Herstellernummer",
            "Inventarnummer", "Barcode", "Kategorie", "Standort", "Anschaffungsdatum",
            "Ende-Datum", "Status", "Prüfstatus",
        };
        sb.AppendLine(string.Join(';', header.Select(Escape)));

        foreach (var a in articles)
        {
            var row = new[]
            {
                a.Identification,
                a.Manufacturer ?? "",
                a.Type ?? "",
                a.SerialNumber ?? "",
                a.ManufacturerNumber ?? "",
                a.InventoryNumber ?? "",
                a.Barcode ?? "",
                a.Category?.Name ?? "",
                a.Location?.Name ?? "",
                a.AcquisitionDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                a.EndDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "",
                a.IsActive ? "aktiv" : "inaktiv",
                InspectionResultInfo.Label(a.CurrentInspectionStatus),
            };
            sb.AppendLine(string.Join(';', row.Select(Escape)));
        }

        // UTF-8 BOM, damit Excel Umlaute korrekt darstellt.
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        return preamble.Concat(content).ToArray();
    }

    private static string Escape(string value)
    {
        // Schutz vor CSV-Formel-Injection: führende Formel-Zeichen mit Apostroph neutralisieren,
        // damit Excel Zellen wie "=HYPERLINK(...)" nicht als Formel ausführt.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
