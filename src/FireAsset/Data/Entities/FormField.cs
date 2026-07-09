namespace FireAsset.Data.Entities;

/// <summary>
/// Einzelnes Feld innerhalb einer Formularversion.
/// </summary>
public class FormField
{
    public int Id { get; set; }

    public int FormVersionId { get; set; }

    public FormVersion FormVersion { get; set; } = default!;

    public string Label { get; set; } = string.Empty;

    public FieldType FieldType { get; set; }

    /// <summary>Reihenfolge der Anzeige innerhalb des Formulars.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optionaler Referenzwert (z. B. Sollwert) für den Vergleich.</summary>
    public string? ReferenceValue { get; set; }

    /// <summary>Optionale Einheit (z. B. "bar", "cm").</summary>
    public string? Unit { get; set; }

    /// <summary>Ob bei der Erfassung der Wert der letzten Prüfung angezeigt werden darf.</summary>
    public bool ShowLastValue { get; set; }

    public ICollection<ProtocolFieldValue> Values { get; set; } = new List<ProtocolFieldValue>();
}
