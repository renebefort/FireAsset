namespace FireAsset.Data.Entities;

/// <summary>
/// Gespeichertes Dokument (freier Brief oder Verwendungsnachweis). Beginnt als Entwurf und kann
/// abgeschlossen werden; ein Verwendungsnachweis löst beim Abschluss Bestandsänderungen aus
/// (Umbuchung der erfassten Artikel, Stilllegung von FTZ-Pool-Geräten). Abgeschlossene Dokumente
/// sind schreibgeschützt.
/// </summary>
public class Document
{
    public int Id { get; set; }

    /// <summary>Optionaler Verweis auf die verwendete Vorlage (rein informativ, kann null sein).</summary>
    public int? TemplateId { get; set; }

    public DocumentTemplate? Template { get; set; }

    public DocumentType Type { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Entwurf;

    // --- Gemeinsame Abschnitte ---
    public string? Title { get; set; }
    public string? Recipient { get; set; }
    public string? Sender { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string? Signature { get; set; }

    // --- Verwendungsnachweis-Felder (nur Typ Verwendungsnachweis) ---
    public UsageKind? UsageKind { get; set; }

    /// <summary>Verwendungszweck und Ort (z. B. "Brennender PKW, Heuchelheim").</summary>
    public string? UsagePurpose { get; set; }

    public DateTime? UsageDate { get; set; }

    public DateTime? OrderDate { get; set; }

    /// <summary>Zielstandort, auf den die erfassten Artikel beim Abschluss umgebucht werden.</summary>
    public int? TargetLocationId { get; set; }

    public Location? TargetLocation { get; set; }

    public string? Remarks { get; set; }

    public ICollection<DocumentArticle> Articles { get; set; } = new List<DocumentArticle>();

    // --- Audit ---
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedByUserId { get; set; }
    public User? ModifiedByUser { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }
}
