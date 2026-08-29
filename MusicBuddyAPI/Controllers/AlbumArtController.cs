using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlbumArtController : ControllerBase
{
    private const string CacheControlHeader = "public, max-age=604800";

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

        byte[] bytes;
        string mimeType;
        string etag;
        if (result is null)
        {
            bytes = AlbumArtExtractor.PlaceholderPng;
            mimeType = "image/png";
            etag = AlbumArtExtractor.PlaceholderEtag;
        }
        else
        {
            bytes = result.Value.Bytes;
            mimeType = result.Value.MimeType;
            etag = result.Value.Etag;
        }

        Response.Headers.CacheControl = CacheControlHeader;
        Response.Headers.ETag = etag;

        var incoming = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(incoming) && incoming == etag)
            return StatusCode(StatusCodes.Status304NotModified);

        return File(bytes, mimeType);
    }
}