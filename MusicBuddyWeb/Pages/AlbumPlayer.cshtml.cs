using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class AlbumPlayerModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<AlbumPlayerModel> _logger;

    public AlbumPlayerModel(IHttpClientFactory http, ILogger<AlbumPlayerModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    public TrackViewModel TrackModel { get; set; } = new();
    public List<string> RandomArtists { get; set; } = new();
    public List<string> RandomGenres { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? artist, string? album, bool random, string? genre)
    {
        await LoadRandomListsAsync();

        if (random)
        {
            return await LoadRandomTrackAsync(artist, genre);
        }

        if (string.IsNullOrEmpty(artist))
        {
            return RedirectToPage("/Albums");
        }

        var catalog = await GetCatalogAsync();
        if (catalog is null) return NotFound();

        var artistData = catalog.Artists.FirstOrDefault(a => a.Name == artist);
        if (artistData is null) return NotFound();

        if (artistData.IsUncatalogued)
        {
            var files = await BrowseRootAsync();
            TrackModel = new TrackViewModel
            {
                Artist = artistData,
                AlbumName = "Uncatalogued",
                Tracks = files?.Select(f => new TrackInfo
                {
                    Name = f.Name,
                    Path = f.Path,
                    DurationSeconds = 0,
                    ChannelCount = f.ChannelCount > 0 ? f.ChannelCount : 2
                }).ToList() ?? new()
            };
            return Page();
        }

        if (string.IsNullOrEmpty(album))
        {
            var firstAlbum = artistData.Albums.FirstOrDefault();
            if (firstAlbum is null) return NotFound();
            album = firstAlbum.Path;
        }

        var detail = await GetAlbumDetailAsync(album);
        if (detail is null) return NotFound();

        TrackModel = new TrackViewModel
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
        };
        return Page();
    }

    private async Task<IActionResult> LoadRandomTrackAsync(string? artist, string? genre)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(artist)) qs.Add($"artist={Uri.EscapeDataString(artist)}");
        if (!string.IsNullOrEmpty(genre)) qs.Add($"genre={Uri.EscapeDataString(genre)}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";

        try
        {
            var result = await client.GetFromJsonAsync<RandomTrackResult>($"/api/random{query}");
            if (result is null)
            {
                TrackModel = new TrackViewModel { AlbumName = "No random track found" };
                return Page();
            }

            var catalog = await GetCatalogAsync();
            var artistData = catalog?.Artists.FirstOrDefault(a => a.Name == result.ArtistName);

            if (string.IsNullOrEmpty(result.AlbumPath))
            {
                var files = await BrowseRootAsync();
                var tracks = files?.Select(f => new TrackInfo
                {
                    Name = f.Name,
                    Path = f.Path,
                    DurationSeconds = 0,
                    ChannelCount = f.ChannelCount > 0 ? f.ChannelCount : 2
                }).ToList() ?? new();

                TrackModel = new TrackViewModel
                {
                    Artist = artistData ?? new ArtistDto { Name = result.ArtistName, Path = result.ArtistName, IsUncatalogued = true },
                    AlbumName = result.ArtistName,
                    Tracks = tracks,
                    RandomTrackIndex = result.TrackIndex,
                    RandomParams = BuildRandomParams(artist, genre)
                };
                return Page();
            }

            var detail = await GetAlbumDetailAsync(result.AlbumPath);
            if (detail is null)
            {
                TrackModel = new TrackViewModel { AlbumName = "Album not found" };
                return Page();
            }

            if (artistData is null)
                artistData = new ArtistDto { Name = result.ArtistName, Path = result.ArtistName };

            TrackModel = new TrackViewModel
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
            };
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random track");
            TrackModel = new TrackViewModel { AlbumName = "Error loading random track" };
            return Page();
        }
    }

    private async Task LoadRandomListsAsync()
    {
        var client = _http.CreateClient("MusicBuddyAPI");

        try
        {
            var catalog = await GetCatalogAsync();
            RandomArtists = catalog?.Artists.Where(a => !a.IsUncatalogued).Select(a => a.Name).OrderBy(n => n).ToList() ?? new();
        }
        catch
        {
            RandomArtists = new();
        }

        try
        {
            var genres = await client.GetFromJsonAsync<List<string>>("/api/random/genres");
            RandomGenres = genres ?? new();
        }
        catch
        {
            RandomGenres = new();
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
    public int ChannelCount { get; set; } = 2;

    public string DisplayTitle => string.IsNullOrEmpty(Title) ? Name : Title;
    public string DisplayDuration => DurationSeconds <= 0 ? "--:--" :
        DurationSeconds >= 3600
            ? $"{DurationSeconds / 3600}:{(DurationSeconds % 3600 / 60):D2}:{DurationSeconds % 60:D2}"
            : $"{DurationSeconds / 60}:{DurationSeconds % 60:D2}";
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
