using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RandomController : ControllerBase
{
    private readonly RandomTrackService _random;
    private readonly ILogger<RandomController> _logger;

    public RandomController(RandomTrackService random, ILogger<RandomController> logger)
    {
        _random = random;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRandom(
        [FromQuery] string? artist,
        [FromQuery] string? genre)
    {
        try
        {
            RandomTrackResult? result;

            if (!string.IsNullOrWhiteSpace(genre))
            {
                result = await _random.GetRandomByGenreAsync(genre);
            }
            else if (!string.IsNullOrWhiteSpace(artist))
            {
                result = await _random.GetRandomByArtistAsync(artist);
            }
            else
            {
                result = await _random.GetRandomAsync();
            }

            if (result is null)
                return NotFound(new { message = "No tracks found matching criteria" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error picking random track");
            return StatusCode(500, new { message = "Error picking random track" });
        }
    }

    [HttpGet("genres")]
    public async Task<IActionResult> GetGenres()
    {
        try
        {
            var genres = await _random.GetGenresAsync();
            return Ok(genres);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading genres");
            return StatusCode(500, new { message = "Error loading genres" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        try
        {
            await _random.RefreshAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing random track index");
            return StatusCode(500, new { message = "Error refreshing index" });
        }
    }
}
