namespace FireAsset.Data.Entities;

/// <summary>
/// Artikel (Gerät oder Gerätegruppe). Zentrale Stammdaten-Entität.
/// </summary>
public class Article
{
    public int Id { get; set; }

    /// <summary>Identifikation / Name des Artikels.</summary>
    public string Identification { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Type { get; set; }

    public string? SerialNumber { get; set; }

    public string? ManufacturerNumber { get; set; }

    public string? InventoryNumber { get; set; }

    public string? Barcode { get; set; }

    /// <summary>Anschaffungsdatum – Basis für die Berechnung der ersten Fälligkeit.</summary>
    public DateTime AcquisitionDate { get; set; }

    public DateTime? ProductionDate { get; set; }

    public DateTime? DecommissionDate { get; set; }

    public string? LegalBasis { get; set; }

    /// <summary>Ende-Datum: nach diesem Datum werden keine Aufgaben mehr erzeugt.</summary>
    public DateTime? EndDate { get; set; }

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = default!;

    public int? LocationId { get; set; }

    public Location? Location { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Aktueller Prüfstatus = Ergebnis des zuletzt erstellten Protokolls.</summary>
    public InspectionResult? CurrentInspectionStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public int? ModifiedByUserId { get; set; }

    public User? ModifiedByUser { get; set; }

    public ICollection<InspectionTask> Tasks { get; set; } = new List<InspectionTask>();

    public ICollection<InspectionProtocol> Protocols { get; set; } = new List<InspectionProtocol>();
}
