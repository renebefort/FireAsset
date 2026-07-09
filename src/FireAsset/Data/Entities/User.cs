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

    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
