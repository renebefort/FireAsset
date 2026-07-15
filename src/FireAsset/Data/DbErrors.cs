using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Data;

/// <summary>
/// Hilfsfunktionen zur Auswertung von Datenbankfehlern (SQL Server), damit Constraint-Verletzungen
/// als verständliche Meldungen statt als unbehandelte Exceptions beim Benutzer ankommen.
/// </summary>
public static class DbErrors
{
    /// <summary>Standardmeldung bei Konflikt durch gleichzeitige Bearbeitung (Concurrency-Token).</summary>
    public const string ConcurrencyMessage =
        "Der Datensatz wurde zwischenzeitlich von einem anderen Benutzer geändert. " +
        "Bitte den Dialog erneut öffnen und die Änderung wiederholen.";

    /// <summary>
    /// True, wenn die Exception eine UNIQUE-Constraint-Verletzung ist – optional eingeschränkt
    /// auf eine Spalte in "Tabelle.Spalte"-Schreibweise (z. B. "Users.Email").
    /// SQL Server meldet 2627 (Unique-Constraint) bzw. 2601 (Unique-Index) und nennt in der
    /// Meldung Objekt ('dbo.Users') und Index ('IX_Users_Email') getrennt – deshalb wird jeder
    /// per '.' getrennte Bestandteil einzeln im Meldungstext gesucht.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException ex, string? column = null)
    {
        if (ex.InnerException is not SqlException sql ||
            (sql.Number != 2627 && sql.Number != 2601))
        {
            return false;
        }
        if (column is null)
        {
            return true;
        }
        return column
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(part => sql.Message.Contains(part, StringComparison.OrdinalIgnoreCase));
    }
}
