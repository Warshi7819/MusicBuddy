using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumArtController : ControllerBase
{
    private readonly AlbumArtExtractor _extractor;

    public AlbumArtController(AlbumArtExtractor extractor)
    {
        _extractor = extractor;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
            return BadRequest(new { message = "path is required" });

        var result = await _extractor.GetAsync(path, HttpContext.RequestAborted);
        if (result is null) return NoContent();

        Response.Headers["Cache-Control"] = "public, max-age=86400";
        return File(result.Value.Bytes, result.Value.MimeType);
    }
}