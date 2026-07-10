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

// Blazor (Interactive Server).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Radzen UI-Komponenten und Dienste (Dialog, Notification, Tooltip, ContextMenu).
builder.Services.AddRadzenComponents();

// Datenbank (SQLite).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=fireasset.db";
// DbContextFactory: kurzlebige Kontexte je Operation (empfohlen für Blazor Server).
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

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
builder.Services.AddSingleton<LoginThrottleService>();
builder.Services.AddScoped<ChangelogService>();

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
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
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
app.UseHttpsRedirection();

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

app.Run();
