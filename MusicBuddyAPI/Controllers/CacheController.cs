using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class CacheController : ControllerBase
{
    private readonly FileCacheService _cache;

    public CacheController(FileCacheService cache) => _cache = cache;

    [HttpPost("refresh")]
    public IActionResult Refresh([FromQuery] string type = "sid")
    {
        _cache.RefreshCache(type);
        return Ok(new { message = $"Cache refreshed for {type}" });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { message = "Cache service active" });
    }
}
