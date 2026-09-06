using System.Net.Http.Json;
using System.Text.Json;
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
    public string? SelectedAlbum { get; set; }

    public string InitialHandler { get; set; } = "ArtistGrid";
    public string? InitialQuery { get; set; }

    public void OnGet(string? artist, string? album)
    {
        SelectedArtist = artist;
        SelectedAlbum = album;
        if (artist is not null)
        {
            InitialHandler = album is not null ? "TrackView" : "AlbumGrid";
            InitialQuery = $"&artist={Uri.EscapeDataString(artist)}";
            if (album is not null) InitialQuery += $"&album={Uri.EscapeDataString(album)}";
        }
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

    public async Task<IActionResult> OnGetTrackViewAsync(string artist, string? album)
    {
        var catalog = await GetCatalogAsync();
        if (catalog is null) return NotFound();

        var artistData = catalog.Artists.FirstOrDefault(a => a.Name == artist);
        if (artistData is null) return NotFound();

        var isUncatalogued = artistData.IsUncatalogued;

        if (isUncatalogued)
        {
            var files = await BrowseRootAsync();
            if (files is null || files.Count == 0)
                return Partial("Albums/_TrackView", new TrackViewModel
                {
                    Artist = artistData,
                    AlbumName = "Uncatalogued",
                    Tracks = new()
                });

            return Partial("Albums/_TrackView", new TrackViewModel
            {
                Artist = artistData,
                AlbumName = "Uncatalogued",
                Tracks = files.Select(f => new TrackInfo
                {
                    Name = f.Name,
                    Path = f.Path,
                    DurationSeconds = 0
                }).ToList()
            });
        }

        if (string.IsNullOrEmpty(album))
        {
            var firstAlbum = artistData.Albums.FirstOrDefault();
            if (firstAlbum is null) return NotFound();
            album = firstAlbum.Path;
        }

        var detail = await GetAlbumDetailAsync(album);
        if (detail is null) return NotFound();

        return Partial("Albums/_TrackView", new TrackViewModel
        {
            Artist = artistData,
            AlbumName = detail.Name,
            AlbumArtist = detail.AlbumArtist,
            Year = detail.Year,
            Genre = detail.Genre,
            Tracks = detail.Tracks.Select(t => new TrackInfo
            {
                Name = t.Name,
                Path = t.Path,
                DurationSeconds = t.DurationSeconds,
                Title = t.Title,
                Artist = t.Artist
            }).ToList()
        });
    }

    public async Task<IActionResult> OnGetRandomTrackViewAsync(string? artist, string? genre)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(artist)) qs.Add($"artist={Uri.EscapeDataString(artist)}");
        if (!string.IsNullOrEmpty(genre)) qs.Add($"genre={Uri.EscapeDataString(genre)}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";

        try
        {
            var result = await client.GetFromJsonAsync<RandomTrackResult>($"/api/random{query}");
            if (result is null) return Content("<div class='text-muted py-4'>No random track found.</div>");

            var catalog = await GetCatalogAsync();
            var artistData = catalog?.Artists.FirstOrDefault(a => a.Name == result.ArtistName);

            if (string.IsNullOrEmpty(result.AlbumPath))
            {
                var files = await BrowseRootAsync();
                var tracks = files?.Select(f => new TrackInfo { Name = f.Name, Path = f.Path, DurationSeconds = 0 }).ToList() ?? new();
                return Partial("Albums/_TrackView", new TrackViewModel
                {
                    Artist = artistData ?? new ArtistDto { Name = result.ArtistName, Path = result.ArtistName, IsUncatalogued = true },
                    AlbumName = result.ArtistName,
                    Tracks = tracks,
                    RandomTrackIndex = result.TrackIndex,
                    RandomParams = BuildRandomParams(artist, genre)
                });
            }

            var detail = await GetAlbumDetailAsync(result.AlbumPath);
            if (detail is null) return Content("<div class='text-muted py-4'>Album not found.</div>");

            if (artistData is null)
                artistData = new ArtistDto { Name = result.ArtistName, Path = result.ArtistName };

            return Partial("Albums/_TrackView", new TrackViewModel
            {
                Artist = artistData,
                AlbumName = detail.Name,
                AlbumArtist = detail.AlbumArtist,
                Year = detail.Year,
                Genre = detail.Genre,
                Tracks = detail.Tracks.Select(t => new TrackInfo
                {
                    Name = t.Name,
                    Path = t.Path,
                    DurationSeconds = t.DurationSeconds,
                    Title = t.Title,
                    Artist = t.Artist
                }).ToList(),
                RandomTrackIndex = result.TrackIndex,
                RandomParams = BuildRandomParams(artist, genre)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random track");
            return Content("<div class='text-danger py-4'>Error loading random track.</div>");
        }
    }

    public async Task<IActionResult> OnGetRandomArtistListAsync()
    {
        var catalog = await GetCatalogAsync();
        var artists = catalog?.Artists.Where(a => !a.IsUncatalogued).Select(a => a.Name).OrderBy(n => n).ToList() ?? new();
        return Partial("Albums/_RandomArtistList", artists);
    }

    public async Task<IActionResult> OnGetRandomGenreListAsync()
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var genres = await client.GetFromJsonAsync<List<string>>("/api/random/genres");
            return Partial("Albums/_RandomGenreList", genres ?? new());
        }
        catch
        {
            return Partial("Albums/_RandomGenreList", new List<string>());
        }
    }

    private string? BuildRandomParams(string? artist, string? genre)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(artist)) parts.Add($"artist={Uri.EscapeDataString(artist)}");
        if (!string.IsNullOrEmpty(genre)) parts.Add($"genre={Uri.EscapeDataString(genre)}");
        return parts.Count > 0 ? string.Join("&", parts) : null;
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

    private async Task<AlbumDetail?> GetAlbumDetailAsync(string path)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            return await client.GetFromJsonAsync<AlbumDetail>($"/api/albums/album?path={Uri.EscapeDataString(path)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading album detail for {Path}", path);
            return null;
        }
    }

    private async Task<List<FileEntry>?> BrowseRootAsync()
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var listing = await client.GetFromJsonAsync<DirectoryListing>("/api/filebrowser/browse?type=mp3&path=");
            return listing?.Files;
        }
        catch
        {
            return null;
        }
    }
}

// DTOs matching the API responses
public class CatalogData
{
    public List<ArtistDto> Artists { get; set; } = new();
}

public class ArtistDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsUncatalogued { get; set; }
    public int AlbumCount { get; set; }
    public List<AlbumDto> Albums { get; set; } = new();
}

public class AlbumDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public string FirstTrackPath { get; set; } = string.Empty;
}

public class AlbumDetail
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? AlbumArtist { get; set; }
    public uint? Year { get; set; }
    public string? Genre { get; set; }
    public List<TrackDto> Tracks { get; set; } = new();
}

public class TrackDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
}

public class RandomTrackResult
{
    public string ArtistName { get; set; } = string.Empty;
    public string? AlbumPath { get; set; }
    public int TrackIndex { get; set; }
}

public class DirectoryListing
{
    public List<DirEntry> Directories { get; set; } = new();
    public List<FileEntry> Files { get; set; } = new();
}

public class DirEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class FileEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public int ChannelCount { get; set; }
}

// View models for partials
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

public class TrackViewModel
{
    public ArtistDto Artist { get; set; } = new();
    public string AlbumName { get; set; } = string.Empty;
    public string? AlbumArtist { get; set; }
    public uint? Year { get; set; }
    public string? Genre { get; set; }
    public List<TrackInfo> Tracks { get; set; } = new();
    public int RandomTrackIndex { get; set; } = -1;
    public string? RandomParams { get; set; }
}

public class TrackInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }

    public string DisplayTitle => string.IsNullOrEmpty(Title) ? Name : Title;
    public string DisplayDuration => DurationSeconds <= 0 ? "--:--" :
        DurationSeconds >= 3600
            ? $"{DurationSeconds / 3600}:{(DurationSeconds % 3600 / 60):D2}:{DurationSeconds % 60:D2}"
            : $"{DurationSeconds / 60}:{DurationSeconds % 60:D2}";
}
