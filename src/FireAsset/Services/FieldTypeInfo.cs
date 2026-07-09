using FireAsset.Data.Entities;

namespace FireAsset.Services;

/// <summary>Anzeigenamen und Hilfsfunktionen für Feldtypen.</summary>
public static class FieldTypeInfo
{
    public record Option(FieldType Value, string Label);

    public static readonly Option[] All =
    {
        new(FieldType.YesNo, "Ja/Nein"),
        new(FieldType.SingleLineText, "Text (einzeilig)"),
        new(FieldType.Integer, "Ganzzahl"),
        new(FieldType.Decimal, "Gleitkommazahl (2 Nachkommastellen)"),
        new(FieldType.MultilineText, "Text (mehrzeilig)"),
        new(FieldType.Date, "Datum"),
    };

    public static string Label(FieldType type) =>
        All.FirstOrDefault(o => o.Value == type)?.Label ?? type.ToString();

    /// <summary>Ob für diesen Feldtyp Referenzwert/Einheit sinnvoll sind (numerische Felder).</summary>
    public static bool SupportsReference(FieldType type) =>
        type is FieldType.Integer or FieldType.Decimal;
}
