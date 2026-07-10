using FireAsset.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Data;

/// <summary>
/// Wendet ausstehende Migrationen an und legt beim ersten Start einen initialen
/// Administrator gemäß Konfiguration (Abschnitt "AdminSeed") an.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        await db.Database.MigrateAsync();

        // WAL-Modus (persistiert in der DB-Datei): parallele Lesezugriffe blockieren
        // Schreibzugriffe nicht mehr – vermeidet "database is locked" unter Blazor-Server-Last.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var email = configuration["AdminSeed:Email"] ?? "admin@fireasset.local";
        var password = configuration["AdminSeed:Password"] ?? "ChangeMe!123";
        var firstName = configuration["AdminSeed:FirstName"] ?? "System";
        var lastName = configuration["AdminSeed:LastName"] ?? "Administrator";

        var admin = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);

        db.Users.Add(admin);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Eine zweite Instanz hat den Admin zeitgleich angelegt (Unique-Index E-Mail) – unkritisch.
        }
    }
}
