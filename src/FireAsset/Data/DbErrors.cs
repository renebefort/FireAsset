using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Data;

/// <summary>
/// Hilfsfunktionen zur Auswertung von Datenbankfehlern (SQLite), damit Constraint-Verletzungen
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
    /// auf eine bestimmte Spalte (Angabe als "Tabelle.Spalte", z. B. "Users.Email").
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException ex, string? column = null)
    {
        if (ex.InnerException is not SqliteException sqlite || sqlite.SqliteErrorCode != 19)
        {
            return false;
        }
        return column is null || sqlite.Message.Contains(column, StringComparison.OrdinalIgnoreCase);
    }
}
