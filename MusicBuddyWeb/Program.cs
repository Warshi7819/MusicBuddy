using System.Net.Http.Json;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
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
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".sid"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapRazorPages();

var apiBase = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5277";
var proxyLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ApiProxy");

app.Map("/api/{**path}", async (HttpContext context, string path) =>
{
    var targetUrl = $"{apiBase}/api/{path}{context.Request.QueryString}";
    proxyLogger.LogInformation("Proxy {Method} {Path} -> {TargetUrl}", context.Request.Method, context.Request.Path, targetUrl);

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var authCookie = context.Request.Cookies["MusicBuddyAuth"];
    if (!string.IsNullOrEmpty(authCookie))
        client.DefaultRequestHeaders.Add("Cookie", $"MusicBuddyAuth={authCookie}");

    using var upstream = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

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

        if ((int)response.StatusCode != 204)
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
