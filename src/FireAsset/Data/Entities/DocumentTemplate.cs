namespace FireAsset.Data.Entities;

/// <summary>
/// Stammdaten-Vorlage für ein Dokument. Legt Typ und vorbelegte Abschnitte fest, aus denen beim
/// Anlegen eines neuen Dokuments die editierbaren Werte übernommen werden.
/// </summary>
public class DocumentTemplate
{
    public int Id { get; set; }

    /// <summary>Anzeigename der Vorlage (z. B. "FTZ Atemschutz").</summary>
    public string Name { get; set; } = string.Empty;

    public DocumentType Type { get; set; }

    public bool IsActive { get; set; } = true;

    // --- Vorbelegte Abschnitte (alle optional) ---
    public string? TitleDefault { get; set; }
    public string? RecipientDefault { get; set; }
    public string? SenderDefault { get; set; }

    /// <summary>Betreff-Vorbelegung (nur Typ Brief relevant).</summary>
    public string? SubjectDefault { get; set; }

    /// <summary>Hauptteil-Vorbelegung (nur Typ Brief relevant).</summary>
    public string? BodyDefault { get; set; }

    public string? SignatureDefault { get; set; }

    /// <summary>Standard-Zielstandort für den Verwendungsnachweis (nur Typ Verwendungsnachweis relevant).</summary>
    public int? DefaultTargetLocationId { get; set; }

    public Location? DefaultTargetLocation { get; set; }

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }
}
