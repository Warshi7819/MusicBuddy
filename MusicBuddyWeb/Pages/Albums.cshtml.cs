using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class AlbumsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<AlbumsModel> _logger;

    public AlbumsModel(IHttpClientFactory http, ILogger<AlbumsModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string? SelectedArtist { get; set; }

    public string InitialHandler { get; set; } = "ArtistGrid";
    public string? InitialQuery { get; set; }

    public IActionResult OnGet(string? artist, string? album)
    {
        if (artist is not null && album is not null)
        {
            return Redirect($"/AlbumPlayer?artist={Uri.EscapeDataString(artist)}&album={Uri.EscapeDataString(album)}");
        }

        SelectedArtist = artist;
        if (artist is not null)
        {
            InitialHandler = "AlbumGrid";
            InitialQuery = $"&artist={Uri.EscapeDataString(artist)}";
        }

        return Page();
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

    public async Task<IActionResult> OnGetAlbumGridAsync(string artist)
    {
        var catalog = await GetCatalogAsync();
        if (catalog is null) return NotFound();

        var artistData = catalog.Artists.FirstOrDefault(a => a.Name == artist);
        if (artistData is null) return NotFound();

        var artMap = await GetArtistArtMapAsync();
        return Partial("Albums/_AlbumGrid", new AlbumGridModel
        {
            Artist = artistData,
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

public class AlbumGridModel
{
    public ArtistDto Artist { get; set; } = new();
    public Dictionary<string, string> ArtistArtMap { get; set; } = new();
}
