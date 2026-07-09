namespace FireAsset.Data.Entities;

/// <summary>
/// Prüfprotokoll: Ergebnis einer durchgeführten Prüfung. Referenziert die exakte Formularversion.
/// </summary>
public class InspectionProtocol
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public Article Article { get; set; } = default!;

    /// <summary>Zugehörige Aufgabe (null bei ungeplanter manueller Prüfung).</summary>
    public int? TaskId { get; set; }

    public InspectionTask? Task { get; set; }

    public int FormVersionId { get; set; }

    public FormVersion FormVersion { get; set; } = default!;

    public InspectionResult Result { get; set; }

    public string? Notes { get; set; }

    /// <summary>True, wenn es sich um eine ungeplante manuelle Prüfung handelt.</summary>
    public bool IsUnplanned { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<ProtocolFieldValue> FieldValues { get; set; } = new List<ProtocolFieldValue>();
}
