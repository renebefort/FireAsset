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
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly PasswordHasher<User> _hasher = new();

    public UserService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized && u.IsActive);

        if (user is null)
        {
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
        var normalized = email.Trim().ToLowerInvariant();
        return await db.Users.AnyAsync(u => u.Email.ToLower() == normalized && u.Id != excludeId);
    }

    public async Task CreateAsync(User user, string password)
    {
        await using var db = await _factory.CreateDbContextAsync();
        user.Email = user.Email.Trim();
        user.CreatedAt = DateTime.UtcNow;
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    /// <summary>Aktualisiert Stammdaten. Ist <paramref name="newPassword"/> gesetzt, wird das Passwort neu gehasht.</summary>
    public async Task UpdateAsync(User user, string? newPassword)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Users.FindAsync(user.Id);
        if (existing is null)
        {
            return;
        }

        existing.FirstName = user.FirstName;
        existing.LastName = user.LastName;
        existing.Email = user.Email.Trim();
        existing.IsActive = user.IsActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            existing.PasswordHash = _hasher.HashPassword(existing, newPassword);
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(id);
        if (user is not null)
        {
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> CountAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.CountAsync();
    }
}
