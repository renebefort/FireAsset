namespace FireAsset.Data.Entities;

/// <summary>
/// Eine konkrete, unveränderliche Version eines Formulars. Jede Änderung am Formular
/// erzeugt eine neue Version; Protokolle referenzieren die exakte Version.
/// </summary>
public class FormVersion
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public Form Form { get; set; } = default!;

    public int VersionNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Benutzer, der diese Version erstellt/bearbeitet hat.</summary>
    public int? EditedByUserId { get; set; }

    public User? EditedByUser { get; set; }

    public ICollection<FormField> Fields { get; set; } = new List<FormField>();

    public ICollection<InspectionProtocol> Protocols { get; set; } = new List<InspectionProtocol>();
}
