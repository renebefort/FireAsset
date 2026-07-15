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

    /// <summary>Reiner Hinweistext (nur Anzeige des Bezeichnungstexts, keine Eingabe, kein Wert).</summary>
    Hinweistext = 6,

    /// <summary>Abschnitts-Überschrift zur Gliederung (nur Anzeige, keine Eingabe, kein Wert).</summary>
    Ueberschrift = 7,

    /// <summary>Externer Link (z. B. auf ein PDF im SharePoint). Wird als Text gespeichert.</summary>
    Url = 8,

    /// <summary>Datei-Upload (PDF oder Bild); die Datei wird als Blob in der Datenbank abgelegt.</summary>
    Attachment = 9,
}

/// <summary>Abschließendes Prüfergebnis eines Protokolls.</summary>
public enum InspectionResult
{
    Bestanden = 0,
    Mangelhaft = 1,
    NichtBestanden = 2,
}

/// <summary>Art eines Dokuments.</summary>
public enum DocumentType
{
    /// <summary>Freier Brief (Titel, Empfänger, Absender, Betreff, Hauptteil, Signatur).</summary>
    Brief = 0,

    /// <summary>Verwendungsnachweis mit fester Struktur inkl. erfasster Artikelliste.</summary>
    Verwendungsnachweis = 1,
}

/// <summary>Bearbeitungsstatus eines Dokuments.</summary>
public enum DocumentStatus
{
    /// <summary>Entwurf: frei editierbar, keine Nebenwirkungen.</summary>
    Entwurf = 0,

    /// <summary>Abgeschlossen: schreibgeschützt; Nebenwirkungen (Umbuchung/Stilllegung) sind erfolgt.</summary>
    Abgeschlossen = 1,
}

/// <summary>Art der Verwendung eines Verwendungsnachweises.</summary>
public enum UsageKind
{
    NachEinsatz = 0,
    NachUebung = 1,
    Regelwartung = 2,
}

/// <summary>Status einer Prüfaufgabe (bewusst nicht "TaskStatus", um die Kollision mit System.Threading.Tasks.TaskStatus zu vermeiden).</summary>
public enum InspectionTaskStatus
{
    Neu = 0,
    InBearbeitung = 1,
    Erledigt = 2,

    /// <summary>Manuell stillgelegt: ohne Prüfung geschlossen (kein Protokoll).</summary>
    Stillgelegt = 3,
}
