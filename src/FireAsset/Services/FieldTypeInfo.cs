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
        new(FieldType.Hinweistext, "Hinweistext (nur Anzeige)"),
        new(FieldType.Ueberschrift, "Überschrift (nur Anzeige)"),
        new(FieldType.Url, "URL / Link"),
        new(FieldType.Attachment, "Anhang (PDF/Bild)"),
    };

    public static string Label(FieldType type) =>
        All.FirstOrDefault(o => o.Value == type)?.Label ?? type.ToString();

    /// <summary>Ob für diesen Feldtyp Referenzwert/Einheit sinnvoll sind (numerische Felder).</summary>
    public static bool SupportsReference(FieldType type) =>
        type is FieldType.Integer or FieldType.Decimal;

    /// <summary>
    /// Reine Anzeige-Typen ohne Eingabe und ohne gespeicherten Wert (Hinweistext, Überschrift).
    /// Werden bei Validierung und "letzte Werte" ignoriert.
    /// </summary>
    public static bool IsDisplayOnly(FieldType type) =>
        type is FieldType.Hinweistext or FieldType.Ueberschrift;

    /// <summary>Feld mit Datei-Upload (Blob-Speicherung statt Textwert).</summary>
    public static bool IsAttachment(FieldType type) =>
        type is FieldType.Attachment;

    /// <summary>Ob der Anwender bei der Erfassung einen Textwert eingibt (kein Anzeige-Typ, kein Anhang).</summary>
    public static bool IsTextValue(FieldType type) =>
        !IsDisplayOnly(type) && !IsAttachment(type);
}
