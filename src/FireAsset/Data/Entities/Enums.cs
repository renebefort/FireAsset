namespace FireAsset.Data.Entities;

/// <summary>Feldarten eines dynamischen Prüfformulars.</summary>
public enum FieldType
{
    /// <summary>Ja/Nein.</summary>
    YesNo = 0,

    /// <summary>Einzeiliger Text.</summary>
    SingleLineText = 1,

    /// <summary>Ganzzahl.</summary>
    Integer = 2,

    /// <summary>Gleitkommazahl (2 Nachkommastellen).</summary>
    Decimal = 3,

    /// <summary>Mehrzeiliger Text.</summary>
    MultilineText = 4,

    /// <summary>Datum.</summary>
    Date = 5,
}

/// <summary>Abschließendes Prüfergebnis eines Protokolls.</summary>
public enum InspectionResult
{
    Bestanden = 0,
    Mangelhaft = 1,
    NichtBestanden = 2,
}

/// <summary>Status einer Prüfaufgabe.</summary>
public enum TaskStatus
{
    Neu = 0,
    InBearbeitung = 1,
    Erledigt = 2,
}
