namespace FireAsset.Data.Entities;

/// <summary>
/// Prüfaufgabe für einen Artikel. Wird automatisch aus Intervallen erzeugt oder manuell angelegt.
/// </summary>
public class InspectionTask
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public Article Article { get; set; } = default!;

    /// <summary>Zugrunde liegendes Intervall (null bei manuell angelegter Aufgabe).</summary>
    public int? IntervalId { get; set; }

    public InspectionInterval? Interval { get; set; }

    public int FormId { get; set; }

    public Form Form { get; set; } = default!;

    public DateTime DueDate { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Neu;

    public bool IsManual { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<InspectionProtocol> Protocols { get; set; } = new List<InspectionProtocol>();
}
