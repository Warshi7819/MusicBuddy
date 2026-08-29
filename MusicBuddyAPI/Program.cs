using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MusicBuddyAPI.Data;
using MusicBuddyAPI.Services;
using MusicBuddyShared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FileCacheService>();
builder.Services.AddSingleton<AlbumCatalogService>();
builder.Services.AddSingleton<RandomTrackService>();
builder.Services.AddSingleton<TagLibThrottle>();
builder.Services.AddSingleton<AlbumArtExtractor>();
builder.Services.AddSingleton<ArtistArtPrewarmer>();
builder.Services.AddHostedService<AlbumCacheWarmer>();

builder.Services.AddDbContext<MusicBuddyDbContext>(options =>
    options.UseSqlite("Data Source=musicbuddy.db"));

var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrEmpty(keyPath))
{
    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicBuddy", "keys");
}
else if (keyPath == "~" || keyPath.StartsWith("~/"))
{
    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), keyPath[2..]);
}
keyPath = Environment.ExpandEnvironmentVariables(keyPath);
Directory.CreateDirectory(keyPath);

builder.Services.AddDataProtection()
    .SetApplicationName("MusicBuddy")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MusicBuddyAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicBuddyDbContext>();
    db.Database.Migrate();

    SeedDefaultAdmin(db);
    SeedBuiltInThemes(db);
}

app.Run();

static void SeedDefaultAdmin(MusicBuddyDbContext db)
{
    if (db.Users.Any())
    {
        return;
    }

    var admin = new User
    {
        Username = "admin",
        PasswordHash = new PasswordHasher<User>().HashPassword(null!, "pass"),
        IsAdmin = true,
        CreatedAt = DateTime.UtcNow
    };
    db.Users.Add(admin);
    db.SaveChanges();
}

static void SeedBuiltInThemes(MusicBuddyDbContext db)
{
    if (db.Themes.Any())
    {
        return;
    }

    db.Themes.AddRange(
        new Theme
        {
            Id = 1,
            Name = "Light",
            IsBuiltIn = true,
            BodyBg = "#ffffff",
            BodyColor = "#212529",
            CardBg = "#ffffff",
            CardBorderColor = "rgba(0,0,0,0.125)",
            PrimaryColor = "#0d6efd",
            NavbarBg = "#0d6efd",
            NavbarTextColor = "#ffffff",
            FooterBg = "#f8f9fa",
            MutedColor = "#6c757d"
        },
        new Theme
        {
            Id = 2,
            Name = "Dark",
            IsBuiltIn = true,
            BodyBg = "#212529",
            BodyColor = "#dee2e6",
            CardBg = "#343a40",
            CardBorderColor = "#495057",
            PrimaryColor = "#0d6efd",
            NavbarBg = "#343a40",
            NavbarTextColor = "#ffffff",
            FooterBg = "#2b3035",
            MutedColor = "#adb5bd"
        }
    );
    db.SaveChanges();
}
