using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumArtController : ControllerBase
{
    private readonly FileCacheService _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AlbumArtController> _logger;

    public AlbumArtController(
        FileCacheService cache,
        IMemoryCache memoryCache,
        ILogger<AlbumArtController> logger)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
            return BadRequest(new { message = "path is required" });

        var cacheKey = $"albumart:{path}";
        if (_memoryCache.TryGetValue(cacheKey, out byte[]? cached) && cached is not null)
        {
            if (cached.Length == 0) return NoContent();
            return File(cached, "image/jpeg");
        }

        var fullPath = _cache.ResolveFilePath(path);
        if (fullPath is null)
        {
            _logger.LogWarning("Album art: file not found for path {Path}", path);
            return NoContent();
        }

        try
        {
            using var tagFile = TagLib.File.Create(fullPath);
            if (tagFile.Tag.Pictures.Length == 0)
            {
                _memoryCache.Set(cacheKey, Array.Empty<byte>(), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
                return NoContent();
            }

            var picture = tagFile.Tag.Pictures[0];
            var bytes = picture.Data.Data;
            var mimeType = picture.MimeType ?? "image/jpeg";

            _memoryCache.Set(cacheKey, bytes, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });

            _logger.LogDebug("Album art returned for {Path} ({Size} bytes)", path, bytes.Length);
            return File(bytes, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading album art for {Path}", path);
            return NoContent();
        }
    }
}
