using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileBrowserController : ControllerBase
{
    private readonly FileCacheService _cache;

    public FileBrowserController(FileCacheService cache) => _cache = cache;

    [HttpGet("browse")]
    public async Task<ActionResult<DirectoryListing>> Browse(
        [FromQuery] string path = "",
        [FromQuery] string type = "sid")
    {
        try
        {
            var listing = await _cache.BrowseAsync(path, type);
            return Ok(listing);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error browsing directory", detail = ex.Message });
        }
    }
}
