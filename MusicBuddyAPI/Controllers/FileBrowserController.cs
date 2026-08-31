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
    private readonly ILogger<FileBrowserController> _logger;

    public FileBrowserController(FileCacheService cache, ILogger<FileBrowserController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("browse")]
    public async Task<ActionResult<DirectoryListing>> Browse(
        [FromQuery] string path = "",
        [FromQuery] string type = "sid")
    {
        _logger.LogInformation("File browse request: type={FileType}, path={Path}", type, path);

        try
        {
            var listing = await _cache.BrowseAsync(path, type);
            _logger.LogInformation("File browse OK: type={FileType}, path={Path}, dirs={DirCount}, files={FileCount}",
                type, path, listing.Directories.Count, listing.Files.Count);
            Response.Headers["Cache-Control"] = "private, max-age=600";
            return Ok(listing);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "File browse bad request: type={FileType}, path={Path}", type, path);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File browse error: type={FileType}, path={Path}", type, path);
            return StatusCode(500, new { message = "Error browsing directory", detail = ex.Message });
        }
    }

    [HttpGet("collect")]
    public async Task<ActionResult<List<FileEntry>>> Collect(
        [FromQuery] string path = "",
        [FromQuery] string type = "sid")
    {
        _logger.LogInformation("File collect request: type={FileType}, path={Path}", type, path);

        try
        {
            var files = await _cache.CollectFilesAsync(path, type);
            _logger.LogInformation("File collect OK: type={FileType}, path={Path}, files={FileCount}",
                type, path, files.Count);
            Response.Headers["Cache-Control"] = "private, max-age=600";
            return Ok(files);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "File collect bad request: type={FileType}, path={Path}", type, path);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File collect error: type={FileType}, path={Path}", type, path);
            return StatusCode(500, new { message = "Error collecting files", detail = ex.Message });
        }
    }
}
