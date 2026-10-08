using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using MusicBuddyShared.Dtos;
using MusicBuddyWeb;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddHttpClient();

builder.Services.AddHttpClient("MusicBuddyAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5277");
}).AddHttpMessageHandler<CookieForwardingHandler>();

builder.Services.AddHttpClient("ApiProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();

// Music files require an authenticated session (the app is exposed to the internet).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Music") &&
        context.User?.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next();
});

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".sid"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseRouting();
app.UseAuthorization();
app.UseRateLimiter();

// Session endpoints for API clients (e.g. the Android Auto app). The Razor Login page
// remains the browser flow; both issue the same DataProtection-protected cookie.
app.MapPost("/api/session", async (HttpContext context, IHttpClientFactory factory, LoginRequest request) =>
{
    var client = factory.CreateClient("MusicBuddyAPI");
    var response = await client.PostAsJsonAsync("/api/auth/login", request);

    if (response.StatusCode == HttpStatusCode.Unauthorized)
        return Results.Json(new { message = "Invalid username or password" }, statusCode: StatusCodes.Status401Unauthorized);
    if (response.StatusCode == HttpStatusCode.Forbidden)
        return Results.Json(new { message = "This account has been disabled" }, statusCode: StatusCodes.Status403Forbidden);
    if (!response.IsSuccessStatusCode)
        return Results.Json(new { message = "Login failed. Please try again." }, statusCode: StatusCodes.Status502BadGateway);

    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
    if (result is null)
        return Results.Json(new { message = "Login failed. Please try again." }, statusCode: StatusCodes.Status502BadGateway);

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.Id.ToString()),
        new(ClaimTypes.Name, result.Username)
    };
    if (!string.IsNullOrEmpty(result.Alias))
    {
        claims.Add(new Claim("Alias", result.Alias));
    }
    if (result.IsAdmin)
    {
        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));

    return Results.Json(result);
}).RequireRateLimiting("LoginPolicy");

app.MapDelete("/api/session", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

app.MapRazorPages();

var apiBase = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5277";
var proxyLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ApiProxy");

app.Map("/api/{**path}", async (HttpContext context, string path, IHttpClientFactory factory) =>
{
    var targetUrl = $"{apiBase}/api/{path}{context.Request.QueryString}";
    proxyLogger.LogInformation("Proxy {Method} {Path} -> {TargetUrl}", context.Request.Method, context.Request.Path, targetUrl);

    var client = factory.CreateClient("ApiProxy");

    using var upstream = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

    var authCookie = context.Request.Cookies["MusicBuddyAuth"];
    if (!string.IsNullOrEmpty(authCookie))
        upstream.Headers.TryAddWithoutValidation("Cookie", $"MusicBuddyAuth={authCookie}");

    var ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
    if (!string.IsNullOrEmpty(ifNoneMatch))
        upstream.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

    if (context.Request.ContentLength > 0 || context.Request.ContentType != null)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        upstream.Content = new StringContent(body, Encoding.UTF8, context.Request.ContentType ?? "application/json");
    }

    try
    {
        var response = await client.SendAsync(upstream);
        proxyLogger.LogInformation("Proxy {Method} {Path} -> {StatusCode}", context.Request.Method, context.Request.Path, (int)response.StatusCode);

        context.Response.StatusCode = (int)response.StatusCode;
        if (response.Content.Headers.ContentType != null)
            context.Response.ContentType = response.Content.Headers.ContentType.ToString();

        if (response.Headers.ETag != null)
            context.Response.Headers.ETag = response.Headers.ETag.ToString();

        if (response.Headers.TryGetValues("Cache-Control", out var cc) ||
            response.Content.Headers.TryGetValues("Cache-Control", out cc))
            context.Response.Headers["Cache-Control"] = string.Join(", ", cc);

        if ((int)response.StatusCode != 204 && (int)response.StatusCode != 304)
            await response.Content.CopyToAsync(context.Response.Body);
    }
    catch (Exception ex)
    {
        proxyLogger.LogError(ex, "Proxy error for {Method} {Path} -> {TargetUrl}", context.Request.Method, context.Request.Path, targetUrl);
        context.Response.StatusCode = 502;
        await context.Response.WriteAsJsonAsync(new { message = "API unavailable", detail = ex.Message });
    }
}).RequireAuthorization();

app.Run();
