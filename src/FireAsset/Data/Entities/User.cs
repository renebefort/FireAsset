namespace FireAsset.Data.Entities;

/// <summary>
/// Benutzer für die Anmeldung am Webportal. Passwörter werden ausschließlich gehasht gespeichert.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Administrator-Recht. Administratoren dürfen u. a. einzelne Prüfprotokolle
    /// in der Übersicht löschen.
    /// </summary>
    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Optimistisches Concurrency-Token (wird bei jeder Änderung erhöht).</summary>
    public int Version { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
