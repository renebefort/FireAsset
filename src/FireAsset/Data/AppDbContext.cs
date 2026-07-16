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
    public DbSet<ArticlePhoto> ArticlePhotos => Set<ArticlePhoto>();
    public DbSet<InspectionTask> InspectionTasks => Set<InspectionTask>();
    public DbSet<InspectionProtocol> InspectionProtocols => Set<InspectionProtocol>();
    public DbSet<ProtocolFieldValue> ProtocolFieldValues => Set<ProtocolFieldValue>();
    public DbSet<ProtocolFieldAttachment> ProtocolFieldAttachments => Set<ProtocolFieldAttachment>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentArticle> DocumentArticles => Set<DocumentArticle>();
    public DbSet<ArticleLogEntry> ArticleLogEntries => Set<ArticleLogEntry>();

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
            // Ansprechpartner: Löschung des Benutzers hebt nur die Zuordnung auf (kein Blockieren).
            e.HasOne(c => c.ContactUser)
                .WithMany()
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.SetNull);
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
            e.Property(a => a.PurchasePrice).HasPrecision(18, 2);
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
            e.Ignore(a => a.ContactName);
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

        modelBuilder.Entity<ArticlePhoto>(e =>
        {
            e.Property(p => p.ContentType).HasMaxLength(150).IsRequired();
            e.Property(p => p.Data).IsRequired();
            e.Property(p => p.Thumbnail).IsRequired();
            // Ein Foto je Artikel; wird beim Löschen des Artikels mitgelöscht.
            e.HasIndex(p => p.ArticleId).IsUnique();
            e.HasOne<Article>()
                .WithMany()
                .HasForeignKey(p => p.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProtocolFieldAttachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
            e.Property(a => a.Data).IsRequired();
            // Pro Protokoll und Feld genau eine Datei (verhindert Doppel-Uploads).
            e.HasIndex(a => new { a.ProtocolId, a.FormFieldId }).IsUnique();
            e.HasOne(a => a.Protocol)
                .WithMany(p => p.Attachments)
                .HasForeignKey(a => a.ProtocolId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.FormField)
                .WithMany()
                .HasForeignKey(a => a.FormFieldId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentTemplate>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Type).HasConversion<int>();
            e.Property(t => t.TitleDefault).HasMaxLength(300);
            e.Property(t => t.RecipientDefault).HasMaxLength(1000);
            e.Property(t => t.SenderDefault).HasMaxLength(1000);
            e.Property(t => t.SubjectDefault).HasMaxLength(500);
            e.Property(t => t.BodyDefault).HasMaxLength(8000);
            e.Property(t => t.SignatureDefault).HasMaxLength(1000);
            e.Property(t => t.Version).IsConcurrencyToken();
            e.HasIndex(t => t.Name).IsUnique();
            e.HasOne(t => t.DefaultTargetLocation)
                .WithMany()
                .HasForeignKey(t => t.DefaultTargetLocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.Property(d => d.Type).HasConversion<int>();
            e.Property(d => d.Status).HasConversion<int>();
            e.Property(d => d.UsageKind).HasConversion<int>();
            e.Property(d => d.Title).HasMaxLength(300);
            e.Property(d => d.Recipient).HasMaxLength(1000);
            e.Property(d => d.Sender).HasMaxLength(1000);
            e.Property(d => d.Subject).HasMaxLength(500);
            e.Property(d => d.Body).HasMaxLength(8000);
            e.Property(d => d.Signature).HasMaxLength(1000);
            e.Property(d => d.UsagePurpose).HasMaxLength(500);
            e.Property(d => d.Remarks).HasMaxLength(4000);
            e.Property(d => d.Version).IsConcurrencyToken();
            // Vorlage darf gelöscht werden, ohne das Dokument zu beeinflussen (nur informativer Verweis).
            e.HasOne(d => d.Template)
                .WithMany()
                .HasForeignKey(d => d.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.TargetLocation)
                .WithMany()
                .HasForeignKey(d => d.TargetLocationId)
                .OnDelete(DeleteBehavior.SetNull);
            // ClientSetNull: drei FKs auf Users (Created/Modified/Completed) – mehrere DB-seitige
            // SET-NULL-Pfade zur selben Tabelle lehnt SQL Server ab ("multiple cascade paths").
            e.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(d => d.ModifiedByUser)
                .WithMany()
                .HasForeignKey(d => d.ModifiedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasOne(d => d.CompletedByUser)
                .WithMany()
                .HasForeignKey(d => d.CompletedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            e.HasIndex(d => new { d.Type, d.Status });
        });

        modelBuilder.Entity<DocumentArticle>(e =>
        {
            e.Property(a => a.BarcodeSnapshot).HasMaxLength(100).IsRequired();
            e.Property(a => a.IdentificationSnapshot).HasMaxLength(300).IsRequired();
            e.Property(a => a.CategoryNameSnapshot).HasMaxLength(200).IsRequired();
            e.HasOne(a => a.Document)
                .WithMany(d => d.Articles)
                .HasForeignKey(a => a.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            // Artikel-Löschung hebt nur den Verweis auf; die Snapshot-Werte bleiben lesbar.
            e.HasOne(a => a.Article)
                .WithMany()
                .HasForeignKey(a => a.ArticleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ArticleLogEntry>(e =>
        {
            e.Property(l => l.Action).HasConversion<int>();
            e.Property(l => l.ArticleIdentificationSnapshot).HasMaxLength(300).IsRequired();
            e.Property(l => l.Details).HasMaxLength(1000);
            e.Property(l => l.UserNameSnapshot).HasMaxLength(201);
            e.HasIndex(l => l.Timestamp);
            // Logbuch folgt dem Artikel: wird er (nur Testdaten) gelöscht, verschwindet auch sein Log.
            e.HasOne(l => l.Article)
                .WithMany()
                .HasForeignKey(l => l.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            // Benutzer-Löschung hebt nur den Verweis auf; der Name bleibt als Snapshot erhalten.
            e.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
