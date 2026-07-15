namespace FireAsset.Data.Entities;

/// <summary>
/// Eine im Verwendungsnachweis erfasste Artikelzeile. Enthält neben dem (optionalen) Verweis auf
/// den Artikel Snapshot-Kopien der relevanten Werte, damit ein abgeschlossenes Dokument stabil
/// bleibt, auch wenn der Artikel später umbenannt, umgelagert oder gelöscht wird.
/// </summary>
public class DocumentArticle
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public Document Document { get; set; } = default!;

    /// <summary>Verweis auf den Artikel; wird bei Artikel-Löschung auf null gesetzt (Snapshot bleibt).</summary>
    public int? ArticleId { get; set; }

    public Article? Article { get; set; }

    /// <summary>Barcode des Artikels zum Erfassungszeitpunkt (= "Artikelnummer" im Dokument).</summary>
    public string BarcodeSnapshot { get; set; } = string.Empty;

    public string IdentificationSnapshot { get; set; } = string.Empty;

    public string CategoryNameSnapshot { get; set; } = string.Empty;
}
