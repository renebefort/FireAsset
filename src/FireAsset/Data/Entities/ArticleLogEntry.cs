namespace FireAsset.Data.Entities;

/// <summary>
/// Logbuch-Eintrag zu einem Artikel: protokolliert Anlage, Bearbeitung, Stilllegung und
/// Standortwechsel – wer wann was getan hat. Nur für Administratoren einsehbar.
/// Benutzername und Artikel-Identifikation werden als Snapshot gehalten, damit der Eintrag
/// lesbar bleibt, auch wenn Artikel/Benutzer sich später ändern.
/// </summary>
public class ArticleLogEntry
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public Article? Article { get; set; }

    /// <summary>Identifikation des Artikels zum Zeitpunkt des Eintrags.</summary>
    public string ArticleIdentificationSnapshot { get; set; } = string.Empty;

    public ArticleLogAction Action { get; set; }

    /// <summary>Zusatzinfo (z. B. "Von „A“ nach „B“" beim Standortwechsel).</summary>
    public string? Details { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Name des auslösenden Benutzers zum Zeitpunkt des Eintrags.</summary>
    public string? UserNameSnapshot { get; set; }

    public DateTime Timestamp { get; set; }
}
