namespace FireAsset.Data.Entities;

/// <summary>
/// Prüfintervall einer Kategorie. Definiert Rhythmus (Monate) und das zu verwendende Formular.
/// </summary>
public class InspectionInterval
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public Category Category { get; set; } = default!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Rhythmus in Monaten. Bei einer Eingangskontrolle ohne Bedeutung (0).</summary>
    public int IntervalMonths { get; set; }

    /// <summary>
    /// Eingangskontrolle: Alternative zum Rhythmus. Erzeugt bei der Artikelanlage genau einmal
    /// eine Aufgabe mit dem verlinkten Formular (Fälligkeit = Anlagedatum), ohne Folgeaufgaben.
    /// </summary>
    public bool IsEntryControl { get; set; }

    /// <summary>
    /// Referenziertes Prüfformular. Optional, damit Intervalle vor der Formularpflege
    /// angelegt werden können; für die automatische Aufgabenanlage muss ein Formular gesetzt sein.
    /// </summary>
    public int? FormId { get; set; }

    public Form? Form { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }

    public ICollection<InspectionTask> Tasks { get; set; } = new List<InspectionTask>();
}
