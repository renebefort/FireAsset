using System.Collections.Concurrent;

namespace FireAsset.Services;

/// <summary>
/// Einfache In-Memory-Drosselung fehlgeschlagener Anmeldeversuche je E-Mail-Adresse.
/// Nach <see cref="MaxFailures"/> Fehlversuchen innerhalb des Fensters wird das Konto
/// für <see cref="LockoutDuration"/> gesperrt. Als Singleton registrieren.
/// </summary>
public class LoginThrottleService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class Entry
    {
        public int Failures;
        public DateTime FirstFailureUtc;
        public DateTime? LockedUntilUtc;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>Verbleibende Sperrzeit, falls das Konto aktuell gesperrt ist, sonst null.</summary>
    public TimeSpan? GetLockoutRemaining(string email)
    {
        if (!_entries.TryGetValue(Normalize(email), out var entry) || entry.LockedUntilUtc is null)
        {
            return null;
        }
        var remaining = entry.LockedUntilUtc.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public void RegisterFailure(string email)
    {
        var entry = _entries.GetOrAdd(Normalize(email), _ => new Entry());
        lock (entry)
        {
            var now = DateTime.UtcNow;
            if (entry.Failures == 0 || now - entry.FirstFailureUtc > FailureWindow)
            {
                entry.Failures = 0;
                entry.FirstFailureUtc = now;
            }
            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.LockedUntilUtc = now + LockoutDuration;
                entry.Failures = 0;
            }
        }
    }

    public void RegisterSuccess(string email) => _entries.TryRemove(Normalize(email), out _);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
