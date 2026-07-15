using System.Globalization;
using System.IO.Compression;
using System.Text;
using FireAsset.Data;
using FireAsset.Data.Entities;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Exportiert gefilterte Prüfprotokolle als ZIP: ein PDF je Protokoll, abgelegt in einer
/// Verzeichnisstruktur, die sich aus der Kategorie ableitet (jeder '.' erzeugt eine Ebene).
/// Dateiname: &lt;jjjjmmdd&gt;_&lt;Barcode|bereinigte Identifikation&gt;.pdf.
/// </summary>
public class ProtocolPdfExportService
{
    private static readonly CultureInfo Germany = CultureInfo.GetCultureInfo("de-DE");

    private readonly IDbContextFactory<AppDbContext> _factory;

    public ProtocolPdfExportService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Baut das ZIP mit je einem PDF pro Protokoll für die übergebenen Protokoll-Ids.</summary>
    public async Task<byte[]> BuildZipAsync(IReadOnlyList<int> protocolIds)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var protocols = await db.InspectionProtocols
            .AsNoTracking()
            .Include(p => p.Article).ThenInclude(a => a.Category).ThenInclude(c => c!.ContactUser)
            .Include(p => p.Article).ThenInclude(a => a.Location)
            .Include(p => p.FormVersion).ThenInclude(v => v.Form)
            .Include(p => p.FormVersion).ThenInclude(v => v.Fields)
            .Include(p => p.FieldValues).ThenInclude(fv => fv.FormField)
            .Include(p => p.CreatedByUser)
            .Where(p => protocolIds.Contains(p.Id))
            .ToListAsync();

        // Standort-Pfad benötigt alle Standorte (Hierarchie).
        var locations = await db.Locations.AsNoTracking().ToListAsync();
        var locationsById = locations.ToDictionary(l => l.Id);

        // Anhang-Dateinamen je (Protokoll, Feld) – ohne die Blobs zu laden.
        var attachments = await db.ProtocolFieldAttachments
            .Where(a => protocolIds.Contains(a.ProtocolId))
            .Select(a => new { a.ProtocolId, a.FormFieldId, a.FileName })
            .ToListAsync();
        var attachmentNames = attachments.ToDictionary(a => (a.ProtocolId, a.FormFieldId), a => a.FileName);

        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in protocols)
            {
                var pdf = RenderProtocolPdf(p, locationsById, attachmentNames);

                var dir = BuildDirectory(p.Article.Category?.Name);
                var fileBase = BuildFileBaseName(p);
                var entryPath = UniquePath(usedPaths, dir, fileBase);

                var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
                await using var es = entry.Open();
                await es.WriteAsync(pdf);
            }
        }
        return zipStream.ToArray();
    }

    // --- Verzeichnis / Dateiname -----------------------------------------------------------

    private static string BuildDirectory(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return "_Ohne_Kategorie";
        var parts = categoryName.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeSegment)
            .Where(s => s.Length > 0)
            .ToArray();
        return parts.Length == 0 ? "_Ohne_Kategorie" : string.Join('/', parts);
    }

    private static string BuildFileBaseName(InspectionProtocol p)
    {
        var date = p.CompletedDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var id = !string.IsNullOrWhiteSpace(p.Article.Barcode)
            ? p.Article.Barcode!
            : p.Article.Identification;
        return $"{date}_{SanitizeSegment(id)}";
    }

    /// <summary>Sichert eindeutige Pfade je Verzeichnis (hängt _2, _3 … an bei Kollision).</summary>
    private static string UniquePath(HashSet<string> used, string dir, string fileBase)
    {
        var candidate = $"{dir}/{fileBase}.pdf";
        var n = 1;
        while (!used.Add(candidate))
        {
            n++;
            candidate = $"{dir}/{fileBase}_{n}.pdf";
        }
        return candidate;
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            sb.Append(invalid.Contains(ch) || ch == '/' || ch == '\\' ? '_' : ch);
        }
        var result = sb.ToString().Trim().TrimEnd('.');
        return result.Length == 0 ? "_" : result;
    }

    // --- PDF-Aufbau ------------------------------------------------------------------------

    private static byte[] RenderProtocolPdf(
        InspectionProtocol p,
        IReadOnlyDictionary<int, Location> locationsById,
        IReadOnlyDictionary<(int, int), string> attachmentNames)
    {
        var doc = new Document();
        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 10;

        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = "2cm";
        section.PageSetup.RightMargin = "2cm";
        section.PageSetup.TopMargin = "1.8cm";
        section.PageSetup.BottomMargin = "1.8cm";

        var title = section.AddParagraph("Prüfprotokoll");
        title.Format.Font.Size = 17;
        title.Format.Font.Bold = true;
        var subtitle = section.AddParagraph($"{p.FormVersion.Form.Name} (v{p.FormVersion.VersionNumber})");
        subtitle.Format.Font.Color = Colors.Gray;
        subtitle.Format.SpaceAfter = "12pt";

        // Kopf: ausgefüllte Artikel-Attribute
        AddHeading(section, "Artikel");
        var articleTable = CreateKeyValueTable(section);
        foreach (var (key, value) in BuildArticleAttributes(p.Article, locationsById))
        {
            AddRow(articleTable, key, value);
        }

        // Prüfungs-Metadaten
        AddHeading(section, "Prüfung");
        var metaTable = CreateKeyValueTable(section);
        AddRow(metaTable, "Prüfdatum", p.CompletedDate.ToString("dd.MM.yyyy", Germany));
        AddRow(metaTable, "Erfasst am", p.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Germany));
        AddRow(metaTable, "Erfasst von", InspectorName(p));
        AddRow(metaTable, "Art", p.IsUnplanned ? "Ungeplante Prüfung" : "Geplante Prüfung");

        // Erfasste Werte
        AddHeading(section, "Erfasste Werte");
        var valuesByField = p.FieldValues.ToDictionary(v => v.FormFieldId, v => v.Value);
        foreach (var field in p.FormVersion.Fields.OrderBy(f => f.SortOrder))
        {
            RenderField(section, field, valuesByField, attachmentNames, p.Id);
        }

        // Ergebnis
        var result = section.AddParagraph();
        result.Format.SpaceBefore = "12pt";
        result.Format.Font.Size = 12;
        result.AddFormattedText("Prüfergebnis: ", TextFormat.Bold);
        result.AddFormattedText(InspectionResultInfo.Label(p.Result), TextFormat.Bold);

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    private static IEnumerable<(string Key, string Value)> BuildArticleAttributes(
        Article a, IReadOnlyDictionary<int, Location> locationsById)
    {
        var items = new List<(string, string?)>
        {
            ("Identifikation", a.Identification),
            ("Barcode", a.Barcode),
            ("Inventarnummer", a.InventoryNumber),
            ("Seriennummer", a.SerialNumber),
            ("Herstellernummer", a.ManufacturerNumber),
            ("Hersteller", a.Manufacturer),
            ("Typ / Modell", a.Type),
            ("Einkaufspreis", a.PurchasePrice is decimal pr ? $"{pr.ToString("0.00", Germany)} €" : null),
            ("Kategorie", a.Category?.Name),
            ("Standort", a.LocationId is int lid && locationsById.TryGetValue(lid, out var loc)
                ? LocationService.BuildPath(loc, locationsById) : null),
            ("Ansprechpartner", a.Category?.ContactUser is { } u ? $"{u.FirstName} {u.LastName}".Trim() : null),
            ("Anschaffungsdatum", a.AcquisitionDate.ToString("dd.MM.yyyy", Germany)),
            ("Produktionsdatum", a.ProductionDate?.ToString("dd.MM.yyyy", Germany)),
            ("Ausmusterung", a.DecommissionDate?.ToString("dd.MM.yyyy", Germany)),
            ("Ende-Datum", a.EndDate?.ToString("dd.MM.yyyy", Germany)),
            ("Rechtsgrundlage", a.LegalBasis),
            ("Beschreibung", a.Description),
            ("FTZ-Pool-Gerät", a.IsPoolDevice ? "Ja" : null),
        };
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.Item2))
            .Select(i => (i.Item1, i.Item2!));
    }

    private static void RenderField(
        Section section, FormField field,
        IReadOnlyDictionary<int, string?> valuesByField,
        IReadOnlyDictionary<(int, int), string> attachmentNames, int protocolId)
    {
        switch (field.FieldType)
        {
            case FieldType.Ueberschrift:
                var heading = section.AddParagraph(field.Label);
                heading.Format.Font.Bold = true;
                heading.Format.SpaceBefore = "8pt";
                heading.Format.SpaceAfter = "2pt";
                return;
            case FieldType.Hinweistext:
                var hint = section.AddParagraph(field.Label);
                hint.Format.Font.Color = Colors.Gray;
                return;
        }

        var label = field.Label + (string.IsNullOrEmpty(field.Unit) ? "" : $" ({field.Unit})");
        string value;
        if (field.FieldType == FieldType.Attachment)
        {
            value = attachmentNames.TryGetValue((protocolId, field.Id), out var name) ? name : "—";
        }
        else
        {
            valuesByField.TryGetValue(field.Id, out var raw);
            value = FormatValue(field.FieldType, raw);
        }

        var para = section.AddParagraph();
        para.Format.SpaceBefore = "2pt";
        para.AddFormattedText($"{label}: ", TextFormat.Bold);
        para.AddText(value);
    }

    private static string FormatValue(FieldType type, string? value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        if (type == FieldType.Date &&
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d.ToString("dd.MM.yyyy", Germany);
        }
        return value;
    }

    private static string InspectorName(InspectionProtocol p) =>
        p.CreatedByUserName
        ?? (p.CreatedByUser is User u ? $"{u.FirstName} {u.LastName}".Trim() : "—");

    private static void AddHeading(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 12;
        p.Format.SpaceBefore = "10pt";
        p.Format.SpaceAfter = "3pt";
        p.Format.Borders.Bottom = new Border { Width = 0.5, Color = Colors.LightGray };
    }

    private static Table CreateKeyValueTable(Section section)
    {
        var table = section.AddTable();
        table.AddColumn("5cm");
        table.AddColumn("11cm");
        return table;
    }

    private static void AddRow(Table table, string key, string value)
    {
        var row = table.AddRow();
        var k = row.Cells[0].AddParagraph(key);
        k.Format.Font.Bold = true;
        row.Cells[1].AddParagraph(value);
    }
}
