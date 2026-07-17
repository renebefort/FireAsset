using System.Security.Claims;
using FireAsset.Components;
using FireAsset.Data;
using FireAsset.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Externe, im Betrieb editierbare DB-Konfiguration. Wird nach appsettings.json geladen und
// überschreibt daher den dortigen ConnectionStrings-Abschnitt. Optional, damit Build/Tooling
// (z. B. dotnet ef) auch ohne die Datei laufen; zur Laufzeit wird ein fehlender String unten
// mit einer klaren Meldung abgefangen.
builder.Configuration.AddJsonFile("dbsettings.json", optional: true, reloadOnChange: true);

// Blazor (Interactive Server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Radzen UI-Komponenten und Dienste (Dialog, Notification, Tooltip, ContextMenu).
builder.Services.AddRadzenComponents();

// Datenbank (MS SQL Server). Connection-String kommt aus dbsettings.json (extern, gitignored).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Kein Connection-String konfiguriert. Bitte 'dbsettings.json' anlegen und unter " +
        "ConnectionStrings:DefaultConnection den SQL-Server-String eintragen (Vorlage: dbsettings.example.json).");
}
// DbContextFactory: kurzlebige Kontexte je Operation (empfohlen für Blazor Server).
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlServer(connectionString));

// Anwendungsdienste.
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<FormService>();
builder.Services.AddScoped<TaskGenerationService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<InspectionService>();
builder.Services.AddScoped<ProtocolService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ArticleLogService>();
builder.Services.AddScoped<AppInfoService>();
builder.Services.AddScoped<DocumentTemplateService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<DocumentPdfService>();
builder.Services.AddSingleton<LoginThrottleService>();
builder.Services.AddScoped<ChangelogService>();

// Adaptiver Dialog-Wrapper: deaktiviert Draggable/Resizable auf <= 768px (Touch/Tablet).
builder.Services.AddScoped<AdaptiveDialogService>();

// PDF-Export der Protokolle (PdfSharp/MigraDoc). Nutzt die Windows-Systemschriften (z. B. Arial).
builder.Services.AddScoped<ProtocolPdfExportService>();
PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;

// Ob HTTPS erzwungen wird: steuert das Secure-Flag des Auth-Cookies UND die HTTPS-Weiterleitung.
// Standard: außerhalb der Entwicklung an (Produktivbetrieb sollte HTTPS nutzen). Für einen reinen
// HTTP-Betrieb in einem vertrauenswürdigen internen Netz kann dies über "Security:RequireHttps": false
// (in appsettings.json oder der externen, im Betrieb editierbaren dbsettings.json) abgeschaltet werden.
// Andernfalls würde der Browser das Secure-Cookie bei HTTP-Zugriff über eine IP-Adresse verwerfen
// (localhost ist ein Sonderfall und funktioniert auch über HTTP) – die Anmeldung schlägt dann fehl.
var requireHttps = builder.Configuration.GetValue<bool?>("Security:RequireHttps")
    ?? !builder.Environment.IsDevelopment();

// Authentifizierung: schlanke Cookie-Auth.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "FireAsset.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = requireHttps
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Events = new CookieAuthenticationEvents
        {
            // Cookie-Revalidierung: gelöschte oder deaktivierte Benutzer verlieren ihre
            // Sitzung sofort (nicht erst nach Ablauf des gleitenden 8-Stunden-Cookies).
            OnValidatePrincipal = async context =>
            {
                var idClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var users = context.HttpContext.RequestServices.GetRequiredService<UserService>();
                if (!int.TryParse(idClaim, out var userId) || !await users.IsActiveAsync(userId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Datenbank migrieren und Admin anlegen.
await DbInitializer.InitializeAsync(app.Services, app.Configuration);

// HTTP-Pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// HTTPS-Weiterleitung nur, wenn HTTPS erzwungen wird (siehe requireHttps oben). Im reinen
// HTTP-Betrieb entfällt sie, um wiederholte Warnungen ohne konfigurierten HTTPS-Port zu vermeiden.
if (requireHttps)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Logout-Endpunkt (Cookie löschen und zur Login-Seite umleiten). Antiforgery wird explizit
// validiert; das Token liefert das Logout-Formular im Layout (<AntiforgeryToken />).
app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// CSV-Export der Inventarliste (nur für angemeldete Benutzer).
app.MapGet("/export/inventory.csv", async (ExportService export, int? category, int? location, bool? active) =>
{
    var bytes = await export.BuildInventoryCsvAsync(category, location, active);
    return Results.File(bytes, "text/csv", "inventarliste.csv");
}).RequireAuthorization();

// Anhang eines Protokoll-Felds (PDF/Bild) inline ausliefern – nur für angemeldete Benutzer.
// Inline (ohne Dateiname), damit Bilder im <img> gerendert und PDFs im Browser geöffnet werden.
app.MapGet("/protokolle/{protocolId:int}/anhang/{fieldId:int}", async (int protocolId, int fieldId, ProtocolService protocols, HttpContext http) =>
{
    var att = await protocols.GetAttachmentAsync(protocolId, fieldId);
    if (att is null) return Results.NotFound();
    // Kein MIME-Sniffing: der Browser rendert die Datei ausschließlich als den deklarierten Typ
    // (verhindert, dass z. B. eine als Bild deklarierte Datei als HTML/Skript interpretiert wird).
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(att.Data, att.ContentType);
}).RequireAuthorization();

// Artikel-Foto (Detailvariante bzw. Grid-Thumbnail) inline ausliefern – nur für angemeldete Benutzer.
app.MapGet("/artikel/{id:int}/foto", async (int id, ArticleService articles, HttpContext http) =>
{
    var photo = await articles.GetPhotoAsync(id, thumbnail: false);
    if (photo is null) return Results.NotFound();
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(photo.Data, photo.ContentType);
}).RequireAuthorization();

app.MapGet("/artikel/{id:int}/foto/thumb", async (int id, ArticleService articles, HttpContext http) =>
{
    var photo = await articles.GetPhotoAsync(id, thumbnail: true);
    if (photo is null) return Results.NotFound();
    http.Response.Headers["X-Content-Type-Options"] = "nosniff";
    return Results.File(photo.Data, photo.ContentType);
}).RequireAuthorization();

// Dokument als PDF herunterladen (freier Brief / Verwendungsnachweis) – nur für angemeldete Benutzer.
app.MapGet("/dokumente/{id:int}/pdf", async (int id, DocumentPdfService pdf) =>
{
    var result = await pdf.RenderAsync(id);
    return result is null
        ? Results.NotFound()
        : Results.File(result.Value.Pdf, "application/pdf", result.Value.FileName);
}).RequireAuthorization();

app.Run();
