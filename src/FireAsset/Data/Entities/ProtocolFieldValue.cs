namespace FireAsset.Data.Entities;

/// <summary>
/// Erfasster Wert eines Formularfelds innerhalb eines Protokolls. Der Wert wird typunabhängig
/// als Zeichenkette gespeichert und je nach <see cref="FormField.FieldType"/> interpretiert.
/// </summary>
public class ProtocolFieldValue
{
    public int Id { get; set; }

    public int ProtocolId { get; set; }

    public InspectionProtocol Protocol { get; set; } = default!;

    public int FormFieldId { get; set; }

    public FormField FormField { get; set; } = default!;

    public string? Value { get; set; }
}
