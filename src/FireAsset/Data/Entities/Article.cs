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

    /// <summary>Einkaufspreis in Euro (optional).</summary>
    public decimal? PurchasePrice { get; set; }

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

    /// <summary>
    /// FTZ-Pool-Gerät: einmaliger Prüfzyklus ohne Folgeaufgaben. Beim Abschluss einer Aufgabe
    /// wird keine Folgeaufgabe erzeugt; ist die letzte Aufgabe erledigt/stillgelegt, wird der
    /// Artikel automatisch stillgelegt (inaktiv, Ende-Datum = Datum der letzten Aufgabe).
    /// </summary>
    public bool IsPoolDevice { get; set; }

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

    /// <summary>
    /// Ansprechpartner der Kategorie als "Vorname Nachname" (leer, wenn keiner hinterlegt).
    /// Nicht persistiert; setzt voraus, dass Category inkl. ContactUser geladen ist.
    /// </summary>
    public string ContactName =>
        Category?.ContactUser is { } u ? $"{u.FirstName} {u.LastName}".Trim() : string.Empty;
}
