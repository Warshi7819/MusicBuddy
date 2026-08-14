using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumsController : ControllerBase
{
    private readonly FileCacheService _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AlbumsController> _logger;

    public AlbumsController(
        FileCacheService cache,
        IMemoryCache memoryCache,
        ILogger<AlbumsController> logger)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        const string cacheKey = "albums:all";
        if (_memoryCache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        try
        {
            var rootListing = await _cache.BrowseAsync("", "mp3");
            var artists = new List<ArtistDto>();

            foreach (var dir in rootListing.Directories)
            {
                var albumListing = await _cache.BrowseAsync(dir.Path, "mp3");
                var albums = new List<AlbumDto>();

                foreach (var albumDir in albumListing.Directories)
                {
                    var albumFiles = await _cache.BrowseAsync(albumDir.Path, "mp3");
                    var mp3s = albumFiles.Files;

                    if (mp3s.Count == 0) continue;

                    albums.Add(new AlbumDto
                    {
                        Name = albumDir.Name,
                        Path = albumDir.Path,
                        TrackCount = mp3s.Count,
                        FirstTrackPath = mp3s[0].Path
                    });
                }

                if (albums.Count == 0) continue;

                artists.Add(new ArtistDto
                {
                    Name = dir.Name,
                    Path = dir.Path,
                    AlbumCount = albums.Count,
                    Albums = albums.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()
                });
            }

            var looseFiles = rootListing.Files;

            if (looseFiles.Count > 0)
            {
                artists.Add(new ArtistDto
                {
                    Name = "Uncatalogued",
                    Path = "",
                    IsUncatalogued = true,
                    AlbumCount = 1,
                    Albums = new List<AlbumDto>
                    {
                        new AlbumDto
                        {
                            Name = "Uncatalogued",
                            Path = "",
                            TrackCount = looseFiles.Count,
                            FirstTrackPath = looseFiles[0].Path
                        }
                    }
                });
            }

            artists = artists.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var result = new { artists };
            _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading albums");
            return StatusCode(500, new { message = "Error loading albums" });
        }
    }

    [HttpGet("album")]
    public async Task<IActionResult> GetAlbum([FromQuery] string path)
    {
        path = path.TrimStart('/');
        var cacheKey = $"albums:detail:{path}";
        if (_memoryCache.TryGetValue(cacheKey, out object? cached) && cached is not null)
            return Ok(cached);

        try
        {
            var listing = await _cache.BrowseAsync(path, "mp3");
            var tracks = new List<TrackDto>();
            string? albumArtist = null;
            uint? year = null;
            string? genre = null;

            foreach (var file in listing.Files)
            {
                var fullPath = _cache.ResolveFilePath(file.Path);
                if (fullPath is null) continue;

                TrackDto? track = null;
                try
                {
                    using var tagFile = TagLib.File.Create(fullPath);
                    track = new TrackDto
                    {
                        Name = file.Name,
                        Path = file.Path,
                        DurationSeconds = (int)tagFile.Properties.Duration.TotalSeconds
                    };

                    if (albumArtist is null || year is null || genre is null)
                    {
                        var aa = tagFile.Tag.AlbumArtists?.FirstOrDefault()
                                 ?? tagFile.Tag.Performers?.FirstOrDefault();
                        var g = tagFile.Tag.Genres?.FirstOrDefault();
                        var y = tagFile.Tag.Year > 0 ? tagFile.Tag.Year : (uint?)null;
                        albumArtist ??= string.IsNullOrWhiteSpace(aa) ? null : aa;
                        year ??= y;
                        genre ??= string.IsNullOrWhiteSpace(g) ? null : g;
                    }
                }
                catch
                {
                    track = new TrackDto { Name = file.Name, Path = file.Path, DurationSeconds = 0 };
                }

                tracks.Add(track);
            }

            var result = new AlbumDetailDto
            {
                Name = Path.GetFileName(path.TrimEnd('/')),
                Path = path,
                AlbumArtist = albumArtist,
                Year = year,
                Genre = genre,
                Tracks = tracks
            };

            _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading album detail for {Path}", path);
            return StatusCode(500, new { message = "Error loading album" });
        }
    }
}

public class AlbumDetailDto
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
