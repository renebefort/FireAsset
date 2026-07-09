using FireAsset.Components;
using FireAsset.Data;
using FireAsset.Services;
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

// Logout-Endpunkt (Cookie löschen und zur Login-Seite umleiten).
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

// CSV-Export der Inventarliste (nur für angemeldete Benutzer).
app.MapGet("/export/inventory.csv", async (ExportService export, int? category, int? location, bool? active) =>
{
    var bytes = await export.BuildInventoryCsvAsync(category, location, active);
    return Results.File(bytes, "text/csv", "inventarliste.csv");
}).RequireAuthorization();

app.Run();
