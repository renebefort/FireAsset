namespace FireAsset.Data.Entities;

/// <summary>
/// Prüfformular. Der konkrete Feldaufbau steckt in versionierten <see cref="FormVersion"/>en,
/// damit alte Protokolle konsistent bleiben.
/// </summary>
public class Form
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>Verweis auf die aktuell gültige Version des Formulars.</summary>
    public int? CurrentVersionId { get; set; }

    public FormVersion? CurrentVersion { get; set; }

    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
}
