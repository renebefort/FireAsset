namespace FireAsset.Data.Entities;

/// <summary>
/// Kategorie eines Artikels. Beschreibt die Art des Gerätes und bündelt die Prüfintervalle.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }

    public ICollection<InspectionInterval> Intervals { get; set; } = new List<InspectionInterval>();

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
