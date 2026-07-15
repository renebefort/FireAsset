using System.Globalization;
using FireAsset.Data;
using FireAsset.Data.Entities;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using MigraDocDocument = MigraDoc.DocumentObjectModel.Document;
using EntityDocument = FireAsset.Data.Entities.Document;

namespace FireAsset.Services;

/// <summary>
/// Erzeugt aus einem gespeicherten Dokument ein PDF (A4-Hochformat) – freier Brief oder
/// Verwendungsnachweis. Aufbau analog zum Prüfprotokoll-PDF (MigraDoc).
/// </summary>
public class DocumentPdfService
{
    private static readonly CultureInfo Germany = CultureInfo.GetCultureInfo("de-DE");

    private readonly IDbContextFactory<AppDbContext> _factory;

    public DocumentPdfService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Rendert das Dokument mit der übergebenen Id; null, wenn es nicht (mehr) existiert.</summary>
    public async Task<(byte[] Pdf, string FileName)?> RenderAsync(int documentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var doc = await db.Documents
            .AsNoTracking()
            .Include(d => d.Articles)
            .Include(d => d.TargetLocation)
            .FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return null;

        var locations = await db.Locations.AsNoTracking().ToListAsync();
        var locationsById = locations.ToDictionary(l => l.Id);

        var pdf = doc.Type == DocumentType.Verwendungsnachweis
            ? RenderUsageCertificate(doc, locationsById)
            : RenderLetter(doc);

        var fileName = BuildFileName(doc);
        return (pdf, fileName);
    }

    private static string BuildFileName(EntityDocument doc)
    {
        var prefix = doc.Type == DocumentType.Verwendungsnachweis ? "Verwendungsnachweis" : "Brief";
        var heading = !string.IsNullOrWhiteSpace(doc.Title) ? doc.Title
            : (!string.IsNullOrWhiteSpace(doc.Subject) ? doc.Subject : $"Dokument_{doc.Id}");
        return $"{prefix}_{Sanitize(heading!)}.pdf";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim().TrimEnd('.');
        return result.Length == 0 ? "Dokument" : result;
    }

    // --- Freier Brief ----------------------------------------------------------------------

    private static byte[] RenderLetter(EntityDocument doc)
    {
        var (document, section) = NewDocument();

        if (!string.IsNullOrWhiteSpace(doc.Sender))
        {
            var sender = section.AddParagraph(doc.Sender!);
            sender.Format.Font.Size = 8;
            sender.Format.Font.Color = Colors.Gray;
            sender.Format.SpaceAfter = "10pt";
        }

        if (!string.IsNullOrWhiteSpace(doc.Recipient))
        {
            AddMultiline(section, doc.Recipient!).Format.SpaceAfter = "24pt";
        }

        if (!string.IsNullOrWhiteSpace(doc.Title))
        {
            var title = section.AddParagraph(doc.Title!);
            title.Format.Font.Size = 15;
            title.Format.Font.Bold = true;
            title.Format.SpaceAfter = "8pt";
        }

        if (!string.IsNullOrWhiteSpace(doc.Subject))
        {
            var subject = section.AddParagraph(doc.Subject!);
            subject.Format.Font.Bold = true;
            subject.Format.SpaceAfter = "12pt";
        }

        if (!string.IsNullOrWhiteSpace(doc.Body))
        {
            AddMultiline(section, doc.Body!).Format.SpaceAfter = "24pt";
        }

        if (!string.IsNullOrWhiteSpace(doc.Signature))
        {
            AddMultiline(section, doc.Signature!);
        }

        return Render(document);
    }

    // --- Verwendungsnachweis ---------------------------------------------------------------

    private static byte[] RenderUsageCertificate(EntityDocument doc, IReadOnlyDictionary<int, Location> locationsById)
    {
        var (document, section) = NewDocument();

        var title = section.AddParagraph(string.IsNullOrWhiteSpace(doc.Title) ? "Verwendungsnachweis" : doc.Title!);
        title.Format.Font.Size = 17;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = "12pt";

        var head = CreateKeyValueTable(section);
        AddRow(head, "Empfänger", doc.Recipient);
        AddRow(head, "Absender", doc.Sender);
        AddRow(head, "Art der Verwendung", UsageKindLabel(doc.UsageKind));
        AddRow(head, "Verwendungszweck / Ort", doc.UsagePurpose);
        AddRow(head, "Verwendungsdatum", doc.UsageDate?.ToString("dd.MM.yyyy", Germany));
        AddRow(head, "Auftragsdatum", doc.OrderDate?.ToString("dd.MM.yyyy", Germany));
        AddRow(head, "Zielstandort", doc.TargetLocationId is int lid && locationsById.TryGetValue(lid, out var loc)
            ? LocationService.BuildPath(loc, locationsById)
            : doc.TargetLocation?.Name);

        AddHeading(section, "Erfasste Artikel");
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.LightGray;
        table.AddColumn("5cm");   // Kategorie
        table.AddColumn("9cm");   // Artikelnummern (Barcodes)
        table.AddColumn("2cm");   // Summe

        var header = table.AddRow();
        header.Shading.Color = Colors.WhiteSmoke;
        header.Cells[0].AddParagraph("Kategorie").Format.Font.Bold = true;
        header.Cells[1].AddParagraph("Artikelnummern").Format.Font.Bold = true;
        header.Cells[2].AddParagraph("Anzahl").Format.Font.Bold = true;

        var groups = doc.Articles
            .GroupBy(a => a.CategoryNameSnapshot)
            .OrderBy(g => g.Key, StringComparer.CurrentCulture);
        foreach (var group in groups)
        {
            var barcodes = group.OrderBy(a => a.BarcodeSnapshot, StringComparer.CurrentCulture)
                .Select(a => a.BarcodeSnapshot);
            var row = table.AddRow();
            row.Cells[0].AddParagraph(group.Key);
            row.Cells[1].AddParagraph(string.Join(", ", barcodes));
            row.Cells[2].AddParagraph(group.Count().ToString(CultureInfo.InvariantCulture));
        }

        var totalRow = table.AddRow();
        totalRow.Shading.Color = Colors.WhiteSmoke;
        totalRow.Cells[0].AddParagraph("Gesamt").Format.Font.Bold = true;
        totalRow.Cells[1].AddParagraph("");
        var total = totalRow.Cells[2].AddParagraph(doc.Articles.Count.ToString(CultureInfo.InvariantCulture));
        total.Format.Font.Bold = true;

        if (!string.IsNullOrWhiteSpace(doc.Remarks))
        {
            AddHeading(section, "Bemerkung");
            AddMultiline(section, doc.Remarks!);
        }

        if (!string.IsNullOrWhiteSpace(doc.Signature))
        {
            var sig = AddMultiline(section, doc.Signature!);
            sig.Format.SpaceBefore = "24pt";
        }

        return Render(document);
    }

    // --- Hilfsfunktionen -------------------------------------------------------------------

    private static (MigraDocDocument, Section) NewDocument()
    {
        var document = new MigraDocDocument();
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = "2cm";
        section.PageSetup.RightMargin = "2cm";
        section.PageSetup.TopMargin = "1.8cm";
        section.PageSetup.BottomMargin = "1.8cm";
        return (document, section);
    }

    private static Paragraph AddMultiline(Section section, string text)
    {
        var para = section.AddParagraph();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) para.AddLineBreak();
            para.AddText(lines[i]);
        }
        return para;
    }

    private static string UsageKindLabel(UsageKind? kind) => kind switch
    {
        UsageKind.NachEinsatz => "nach Einsatz",
        UsageKind.NachUebung => "nach Übung",
        UsageKind.Regelwartung => "zur Regelwartung",
        _ => "—",
    };

    private static void AddHeading(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 12;
        p.Format.SpaceBefore = "12pt";
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

    private static void AddRow(Table table, string key, string? value)
    {
        var row = table.AddRow();
        var k = row.Cells[0].AddParagraph(key);
        k.Format.Font.Bold = true;
        row.Cells[1].AddParagraph(string.IsNullOrWhiteSpace(value) ? "—" : value!);
    }

    private static byte[] Render(MigraDocDocument document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }
}
