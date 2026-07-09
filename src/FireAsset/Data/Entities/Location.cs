namespace FireAsset.Data.Entities;

/// <summary>
/// Standort in hierarchischer Struktur (z. B. Fahrzeug > 46 HLF > Kabine).
/// Jeder Standort kann einen übergeordneten Standort besitzen.
/// </summary>
public class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Barcode { get; set; }

    /// <summary>Icon-Kennung für die Darstellung (z. B. "directory", "truck", "shelf", "room").</summary>
    public string? Icon { get; set; }

    public int? ParentLocationId { get; set; }

    public Location? ParentLocation { get; set; }

    public ICollection<Location> Children { get; set; } = new List<Location>();

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}
