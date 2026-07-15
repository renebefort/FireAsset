using FireAsset.Data;
using FireAsset.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Services;

/// <summary>
/// Benutzerverwaltung und Passwort-Hashing (ASP.NET Core PasswordHasher).
/// </summary>
public class UserService
{
    /// <summary>Minimale Passwortlänge für alle Konten.</summary>
    public const int MinPasswordLength = 8;

    public const string PasswordPolicyMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PasswordHasher<User> _hasher = new();

    // Dummy-Hash, damit die Anmeldung bei unbekannter E-Mail gleich lange dauert wie bei
    // bekannter (erschwert Timing-basiertes Ausspähen vorhandener Konten).
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString("N"));

    public UserService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var normalized = email.Trim();
        // E-Mail-Spalte ist NOCASE-kollationiert: Vergleich ist case-insensitiv und nutzt den Index.
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == normalized && u.IsActive);

        if (user is null)
        {
            _hasher.VerifyHashedPassword(new User(), DummyHash, password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        return user;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.FindAsync(id);
    }

    /// <summary>True, wenn der Benutzer existiert und aktiv ist (für die Cookie-Revalidierung).</summary>
    public async Task<bool> IsActiveAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.AnyAsync(u => u.Id == id && u.IsActive);
    }

    public async Task<List<User>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var normalized = email.Trim();
        return await db.Users.AnyAsync(u => u.Email == normalized && u.Id != excludeId);
    }

    /// <summary>Legt einen Benutzer an. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.</summary>
    public async Task<string?> CreateAsync(User user, string password)
    {
        if (password.Length < MinPasswordLength)
        {
            return PasswordPolicyMessage;
        }

        await using var db = await _factory.CreateDbContextAsync();
        user.Email = user.Email.Trim();
        user.CreatedAt = DateTime.UtcNow;
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Users.Email"))
        {
            return "Diese E-Mail wird bereits verwendet.";
        }
        return null;
    }

    /// <summary>
    /// Aktualisiert Stammdaten. Ist <paramref name="newPassword"/> gesetzt, wird das Passwort neu
    /// gehasht. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.
    /// </summary>
    public async Task<string?> UpdateAsync(User user, string? newPassword)
    {
        if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < MinPasswordLength)
        {
            return PasswordPolicyMessage;
        }

        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Users.FindAsync(user.Id);
        if (existing is null)
        {
            return "Der Benutzer existiert nicht mehr.";
        }

        // Server-seitiger Schutz: Der letzte aktive Benutzer darf nicht deaktiviert werden,
        // sonst kann sich niemand mehr anmelden.
        if (existing.IsActive && !user.IsActive &&
            !await db.Users.AnyAsync(u => u.Id != user.Id && u.IsActive))
        {
            return "Der letzte aktive Benutzer kann nicht deaktiviert werden.";
        }

        db.Entry(existing).Property(u => u.Version).OriginalValue = user.Version;
        existing.Version = user.Version + 1;

        existing.FirstName = user.FirstName;
        existing.LastName = user.LastName;
        existing.Email = user.Email.Trim();
        existing.IsActive = user.IsActive;
        existing.IsAdmin = user.IsAdmin;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            existing.PasswordHash = _hasher.HashPassword(existing, newPassword);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return DbErrors.ConcurrencyMessage;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "Users.Email"))
        {
            return "Diese E-Mail wird bereits verwendet.";
        }
        return null;
    }

    /// <summary>Löscht einen Benutzer. Gibt bei Blockade eine Fehlermeldung zurück, sonst null.</summary>
    public async Task<string?> DeleteAsync(int id, int? currentUserId)
    {
        if (id == currentUserId)
        {
            return "Der aktuell angemeldete Benutzer kann nicht gelöscht werden.";
        }

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return null;
        }

        if (user.IsActive && !await db.Users.AnyAsync(u => u.Id != id && u.IsActive))
        {
            return "Der letzte aktive Benutzer kann nicht gelöscht werden.";
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<int> CountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync();
    }
}
