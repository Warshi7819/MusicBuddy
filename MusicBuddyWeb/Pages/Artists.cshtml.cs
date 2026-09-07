using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class ArtistsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ArtistsModel> _logger;

    public ArtistsModel(IHttpClientFactory http, ILogger<ArtistsModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetArtistGridAsync()
    {
        var catalog = await GetCatalogAsync();
        if (catalog is null) return Partial("Albums/_ArtistGrid", new ArtistGridModel { Artists = new() });

        var artMap = await GetArtistArtMapAsync();
        return Partial("Albums/_ArtistGrid", new ArtistGridModel
        {
            Artists = catalog.Artists,
            ArtistArtMap = artMap
        });
    }

    private async Task<CatalogData?> GetCatalogAsync()
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            return await client.GetFromJsonAsync<CatalogData>("/api/albums");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading album catalog");
            return null;
        }
    }

    private async Task<Dictionary<string, string>> GetArtistArtMapAsync()
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            return await client.GetFromJsonAsync<Dictionary<string, string>>("/api/artistart") ?? new();
        }
        catch
        {
            return new();
        }
    }
}

public class ArtistGridModel
{
    public List<ArtistDto> Artists { get; set; } = new();
    public Dictionary<string, string> ArtistArtMap { get; set; } = new();
}
