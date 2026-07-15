using FireAsset.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FireAsset.Data;

/// <summary>
/// Zentraler EF-Core-Kontext für den Asset Manager.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<InspectionInterval> InspectionIntervals => Set<InspectionInterval>();
    public DbSet<Form> Forms => Set<Form>();
    public DbSet<FormVersion> FormVersions => Set<FormVersion>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<InspectionTask> InspectionTasks => Set<InspectionTask>();
    public DbSet<InspectionProtocol> InspectionProtocols => Set<InspectionProtocol>();
    public DbSet<ProtocolFieldValue> ProtocolFieldValues => Set<ProtocolFieldValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            // E-Mail-Vergleiche und der Unique-Index arbeiten case-insensitiv – unter SQL Server
            // deckt das die Standard-Collation (…_CI_AS) bereits ab, daher keine explizite Angabe.
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Version).IsConcurrencyToken();
            e.HasIndex(u => u.Email).IsUnique();
            e.Ignore(u => u.FullName);
        });

        modelBuilder.Entity<Location>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(200).IsRequired();
            e.Property(l => l.Description).HasMaxLength(1000);
            e.Property(l => l.Barcode).HasMaxLength(100);
            e.Property(l => l.Icon).HasMaxLength(50);
            e.Property(l => l.Version).IsConcurrencyToken();
            e.HasIndex(l => l.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.HasOne(l => l.ParentLocation)
                .WithMany(l => l.Children)
                .HasForeignKey(l => l.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Description).HasMaxLength(1000);
            e.Property(c => c.Version).IsConcurrencyToken();
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<InspectionInterval>(e =>
        {
            e.Property(i => i.Name).HasMaxLength(200).IsRequired();
            e.Property(i => i.Version).IsConcurrencyToken();
            e.HasOne(i => i.Category)
                .WithMany(c => c.Intervals)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Form)
                .WithMany()
                .HasForeignKey(i => i.FormId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Form>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(200).IsRequired();
            e.Property(f => f.Description).HasMaxLength(1000);
            e.Property(f => f.Version).IsConcurrencyToken();
            // Aktuelle Version: separate, optionale Beziehung (verhindert Zyklus-Kaskaden).
            e.HasOne(f => f.CurrentVersion)
                .WithMany()
                .HasForeignKey(f => f.CurrentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FormVersion>(e =>
        {
            e.HasOne(v => v.Form)
                .WithMany(f => f.Versions)
                .HasForeignKey(v => v.FormId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.EditedByUser)
                .WithMany()
                .HasForeignKey(v => v.EditedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(v => new { v.FormId, v.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<FormField>(e =>
        {
            e.Property(f => f.Label).HasMaxLength(200).IsRequired();
            e.Property(f => f.ReferenceValue).HasMaxLength(100);
            e.Property(f => f.Unit).HasMaxLength(50);
            e.Property(f => f.FieldType).HasConversion<int>();
            e.HasOne(f => f.FormVersion)
                .WithMany(v => v.Fields)
                .HasForeignKey(f => f.FormVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Article>(e =>
        {
            e.Property(a => a.Identification).HasMaxLength(300).IsRequired();
            e.Property(a => a.Manufacturer).HasMaxLength(200);
            e.Property(a => a.Type).HasMaxLength(200);
            e.Property(a => a.SerialNumber).HasMaxLength(100);
            e.Property(a => a.ManufacturerNumber).HasMaxLength(100);
            e.Property(a => a.InventoryNumber).HasMaxLength(100);
            e.Property(a => a.Barcode).HasMaxLength(100);
            e.Property(a => a.LegalBasis).HasMaxLength(300);
            e.Property(a => a.Description).HasMaxLength(2000);
            e.Property(a => a.CurrentInspectionStatus).HasConversion<int>();
            e.Property(a => a.Version).IsConcurrencyToken();
            e.HasIndex(a => a.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.HasIndex(a => a.InventoryNumber);
            e.HasOne(a => a.Category)
                .WithMany(c => c.Articles)
                .HasForeignKey(a => a.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Location)
                .WithMany(l => l.Articles)
                .HasForeignKey(a => a.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
            // ClientSetNull statt SetNull: Article hat zwei FKs auf Users (Created/Modified);
            // zwei DB-seitige ON DELETE SET NULL-Pfade zur selben Tabelle lehnt SQL Server ab
            // ("multiple cascade paths"). Benutzer werden ohnehin nur soft-deleted (IsActive),
            // daher ist das Nullen dieser Audit-Referenzen auf Client-Ebene ausreichend.
            e.HasOne(a => a.CreatedByUser)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(a => a.ModifiedByUser)
                .WithMany()
                .HasForeignKey(a => a.ModifiedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<InspectionTask>(e =>
        {
            e.Property(t => t.Status).HasConversion<int>();
            e.HasOne(t => t.Article)
                .WithMany(a => a.Tasks)
                .HasForeignKey(t => t.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Interval)
                .WithMany(i => i.Tasks)
                .HasForeignKey(t => t.IntervalId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Form)
                .WithMany()
                .HasForeignKey(t => t.FormId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.Status, t.DueDate });
        });

        modelBuilder.Entity<InspectionProtocol>(e =>
        {
            e.Property(p => p.Result).HasConversion<int>();
            e.Property(p => p.Notes).HasMaxLength(4000);
            e.Property(p => p.CreatedByUserName).HasMaxLength(201);
            // Restrict: Prüfprotokolle sind Nachweisdokumente und dürfen nicht durch
            // Artikel-Löschung kaskadiert vernichtet werden (Absicherung zusätzlich zum Service-Check).
            e.HasOne(p => p.Article)
                .WithMany(a => a.Protocols)
                .HasForeignKey(p => p.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Task)
                .WithMany(t => t.Protocols)
                .HasForeignKey(p => p.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(p => p.FormVersion)
                .WithMany(v => v.Protocols)
                .HasForeignKey(p => p.FormVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProtocolFieldValue>(e =>
        {
            e.Property(v => v.Value).HasMaxLength(4000);
            // Verhindert doppelte Werte für dasselbe Feld innerhalb eines Protokolls (Doppel-Submit).
            e.HasIndex(v => new { v.ProtocolId, v.FormFieldId }).IsUnique();
            e.HasOne(v => v.Protocol)
                .WithMany(p => p.FieldValues)
                .HasForeignKey(v => v.ProtocolId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.FormField)
                .WithMany(f => f.Values)
                .HasForeignKey(v => v.FormFieldId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
