using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetadataController : ControllerBase
{
    private readonly TrackMetadataService _metadata;
    private readonly ILogger<MetadataController> _logger;

    public MetadataController(TrackMetadataService metadata, ILogger<MetadataController> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] string? song = null,
        [FromQuery] string? artist = null,
        [FromQuery] string? genre = null,
        [FromQuery] int max = 100)
    {
        try
        {
            // Legacy single-query mode: if q is provided but no field-specific params, search all fields
            List<TrackMetadata> results;
            if (!string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(song) && string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(genre))
            {
                results = await _metadata.SearchAsync(song: q, maxResults: max);
            }
            else
            {
                results = await _metadata.SearchAsync(song, artist, genre, max);
            }

            return Ok(results.Select(t => new
            {
                t.Id,
                t.FilePath,
                t.UrlPath,
                t.FileName,
                t.FileSize,
                t.TrackName,
                t.Artist,
                t.Album,
                t.Genre,
                t.Year,
                t.DurationSeconds,
                t.ChannelCount,
                t.ArtistFolder
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching metadata");
            return StatusCode(500, new { message = "Error searching metadata" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            await _metadata.StartRefreshAsync();
            return Accepted(new { message = "Refresh started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting metadata refresh");
            return StatusCode(500, new { message = "Error starting refresh" });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        try
        {
            var status = await _metadata.GetStatusAsync();
            var count = await _metadata.GetTrackCountAsync();
            return Ok(new
            {
                status.IsRefreshing,
                status.TotalFiles,
                status.ScannedFiles,
                status.LastRefreshTime,
                status.Error,
                TotalIndexed = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata status");
            return StatusCode(500, new { message = "Error getting status" });
        }
    }
}
