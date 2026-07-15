namespace FireAsset.Data.Entities;

/// <summary>
/// Hochgeladene Datei (PDF oder Bild) zu einem Anhang-Feld innerhalb eines Protokolls.
/// Die Binärdaten liegen bewusst in einer eigenen Tabelle, damit die großen Blobs die
/// Werte- und Listenabfragen nicht belasten. Pro (Protokoll, Feld) genau eine Datei.
/// </summary>
public class ProtocolFieldAttachment
{
    public int Id { get; set; }

    public int ProtocolId { get; set; }

    public InspectionProtocol Protocol { get; set; } = default!;

    public int FormFieldId { get; set; }

    public FormField FormField { get; set; } = default!;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>Dateigröße in Bytes (für Anzeige und Plausibilität).</summary>
    public long SizeBytes { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
}
