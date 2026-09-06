using System.Collections.Concurrent;
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
    private readonly AlbumArtExtractor _artExtractor;
    private readonly ArtistArtPrewarmer _artPrewarmer;
    private readonly ILogger<AlbumsController> _logger;
    private static readonly ConcurrentDictionary<string, byte> _detailCacheKeys = new();

    public AlbumsController(
        AlbumCatalogService catalog,
        FileCacheService cache,
        IMemoryCache memoryCache,
        AlbumArtExtractor artExtractor,
        ArtistArtPrewarmer artPrewarmer,
        ILogger<AlbumsController> logger)
    {
        _catalog = catalog;
        _cache = cache;
        _memoryCache = memoryCache;
        _artExtractor = artExtractor;
        _artPrewarmer = artPrewarmer;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var payload = await _catalog.GetPayloadAsync();
            Response.Headers["Cache-Control"] = "private, max-age=600";
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
            foreach (var key in _detailCacheKeys.Keys.ToList())
            {
                _memoryCache.Remove(key);
                _detailCacheKeys.TryRemove(key, out _);
            }
            _artExtractor.ClearAll();
            _cache.ClearAll("mp3");

            await _catalog.RefreshAsync();
            return Accepted(new { message = "Album catalog refresh started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing albums");
            return StatusCode(500, new { message = "Error refreshing albums" });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        try
        {
            var status = await _catalog.GetStatusAsync();
            var payload = await _catalog.GetPayloadAsync();
            return Ok(new
            {
                status.IsRefreshing,
                status.TotalArtists,
                status.ScannedArtists,
                status.LastRefreshTime,
                status.Error,
                TotalAlbums = payload.Artists.Sum(a => a.AlbumCount)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting album catalog status");
            return StatusCode(500, new { message = "Error getting status" });
        }
    }

    [HttpGet("album")]
    public async Task<IActionResult> GetAlbum([FromQuery] string? path)
    {
        path = (path ?? "").TrimStart('/');
        var cacheKey = $"albums:detail:{path}";
        if (_memoryCache.TryGetValue(cacheKey, out object? cached) && cached is not null)
        {
            Response.Headers["Cache-Control"] = "private, max-age=600";
            return Ok(cached);
        }

        try
        {
            var listing = await _cache.BrowseAsync(path, "mp3");
            var tracks = new List<TrackDto>();
            string? albumArtist = null;
            uint? year = null;
            string? genre = null;

            var ct = HttpContext.RequestAborted;

            foreach (var file in listing.Files)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = _cache.ResolveFilePath(file.Path);
                if (fullPath is null) continue;

                TrackDto? track = null;
                try
                {
                    using var tagFile = TagLib.File.Create(fullPath);
                    var tagTitle = tagFile.Tag.Title;
                    var tagPerformer = tagFile.Tag.Performers?.FirstOrDefault();
                    track = new TrackDto
                    {
                        Name = file.Name,
                        Path = file.Path,
                        DurationSeconds = (int)tagFile.Properties.Duration.TotalSeconds,
                        Title = string.IsNullOrWhiteSpace(tagTitle) ? null : tagTitle,
                        Artist = string.IsNullOrWhiteSpace(tagPerformer) ? null : tagPerformer
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
                    track = new TrackDto { Name = file.Name, Path = file.Path, DurationSeconds = 0, Title = file.Name, Artist = null };
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

            _detailCacheKeys.TryAdd(cacheKey, 0);
            _memoryCache.Set(cacheKey, result);
            Response.Headers["Cache-Control"] = "private, max-age=600";
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return NoContent();
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
    public string? Title { get; set; }
    public string? Artist { get; set; }
}