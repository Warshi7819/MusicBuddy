using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MusicBuddyShared.Dtos;

namespace MusicBuddyAndroid.Api;

public sealed class MusicBuddyAuthException : Exception
{
    public MusicBuddyAuthException(string message) : base(message) { }
}

public sealed class MusicBuddyClient : IDisposable
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    public string BaseUrl { get; private set; } = string.Empty;
    public string Username { get; private set; }
    public string Password { get; private set; }
    public string CookieHeader { get; private set; }

    public MusicBuddyClient()
    {
        _handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All,
            // The API answers unauthenticated requests with a 302 to the web
            // login page; following it turns into a 200 HTML response that
            // explodes in the JSON deserializer. Handle 3xx ourselves instead.
            AllowAutoRedirect = false
        };
        _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public void Configure(string baseUrl, string username, string password)
    {
        baseUrl = baseUrl.Trim().TrimEnd('/');
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "https://" + baseUrl;
        }
        BaseUrl = baseUrl;
        Username = username.Trim();
        Password = password;
    }

    public async Task<LoginResponse> LoginAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(BaseUrl) || string.IsNullOrEmpty(Username))
            throw new InvalidOperationException("Client is not configured");

        await _loginLock.WaitAsync(ct);
        try
        {
            _handler.CookieContainer = new CookieContainer();

            var payload = JsonSerializer.Serialize(new LoginRequest { Username = Username, Password = Password }, Json.Options);
            using var content = new StringContent(payload, Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var response = await _http.PostAsync($"{BaseUrl}/api/session", content, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new MusicBuddyAuthException("Invalid username or password");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new MusicBuddyAuthException("This account is disabled");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(Json.Options, ct)
                         ?? throw new MusicBuddyAuthException("Login failed");
            UpdateCookieHeader();
            return result;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(BaseUrl)) return;
        try
        {
            using var response = await _http.DeleteAsync($"{BaseUrl}/api/session", ct);
        }
        catch
        {
            // best effort - local cookie container is cleared below regardless
        }
        _handler.CookieContainer = new CookieContainer();
        CookieHeader = null;
    }

    public async Task<T> GetJsonAsync<T>(string path, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await _http.GetAsync(BaseUrl + path, ct);
            if ((response.StatusCode == HttpStatusCode.Unauthorized || IsRedirect(response.StatusCode)) && attempt == 0)
            {
                await LoginAsync(ct);
                continue;
            }
            response.EnsureSuccessStatusCode();

            // Guard against proxies that answer 2xx with an HTML error/login
            // page - the JSON deserializer's stack trace for that is useless.
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != null && !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Expected JSON from {path} but got {mediaType} (HTTP {(int)response.StatusCode})");

            return await response.Content.ReadFromJsonAsync<T>(Json.Options, ct)
                   ?? throw new InvalidOperationException($"Empty response from {path}");
        }
        throw new InvalidOperationException($"Request failed after re-auth: {path}");
    }

    public Task<AlbumCatalogPayloadDto> GetCatalogAsync(CancellationToken ct = default)
        => GetJsonAsync<AlbumCatalogPayloadDto>("/api/albums", ct);

    public Task<AlbumDetailDto> GetAlbumAsync(string albumPath, CancellationToken ct = default)
        => GetJsonAsync<AlbumDetailDto>($"/api/albums/album?path={Uri.EscapeDataString(albumPath)}", ct);

    public async Task<byte[]> GetByteArrayAsync(string url, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await _http.GetAsync(url, ct);
            if ((response.StatusCode == HttpStatusCode.Unauthorized || IsRedirect(response.StatusCode)) && attempt == 0)
            {
                await LoginAsync(ct);
                continue;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        throw new InvalidOperationException($"Request failed after re-auth: {url}");
    }

    private static bool IsRedirect(HttpStatusCode status)
        => (int)status is >= 300 and < 400;

    public string ToAbsoluteUrl(string pathAndQuery)
    {
        if (pathAndQuery.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathAndQuery.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return pathAndQuery;
        if (!pathAndQuery.StartsWith('/'))
            pathAndQuery = "/" + pathAndQuery;
        return BaseUrl + pathAndQuery;
    }

    // Percent-encodes each path segment so filenames with spaces/parentheses
    // produce valid stream URLs (the raw API paths are not URL-encoded).
    public string ToStreamUrl(string trackPath)
    {
        var segments = trackPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var encoded = string.Join("/", segments.Select(Uri.EscapeDataString));
        return BaseUrl + "/" + encoded;
    }

    private void UpdateCookieHeader()
    {
        var cookies = _handler.CookieContainer.GetCookies(new Uri(BaseUrl));
        var auth = cookies["MusicBuddyAuth"];
        CookieHeader = auth != null ? $"MusicBuddyAuth={auth.Value}" : null;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
