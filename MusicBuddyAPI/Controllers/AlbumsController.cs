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
    private readonly AlbumCatalogService _catalog;
    private readonly FileCacheService _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AlbumsController> _logger;

    public AlbumsController(
        AlbumCatalogService catalog,
        FileCacheService cache,
        IMemoryCache memoryCache,
        ILogger<AlbumsController> logger)
    {
        _catalog = catalog;
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var payload = await _catalog.GetPayloadAsync();
            return Ok(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading albums");
            return StatusCode(500, new { message = "Error loading albums" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            await _catalog.RefreshAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing albums");
            return StatusCode(500, new { message = "Error refreshing albums" });
        }
    }

    [HttpGet("album")]
    public async Task<IActionResult> GetAlbum([FromQuery] string? path)
    {
        path = (path ?? "").TrimStart('/');
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