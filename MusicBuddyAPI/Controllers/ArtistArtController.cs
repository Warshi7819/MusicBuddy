using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicBuddyAPI.Data;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArtistArtController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public ArtistArtController(MusicBuddyDbContext db) => _db = db;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll()
    {
        return await _db.ArtistArtPreferences
            .Where(a => a.UserId == CurrentUserId)
            .ToDictionaryAsync(a => a.ArtistPath, a => a.AlbumPath);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromQuery] string artistPath, [FromQuery] string albumPath)
    {
        artistPath = artistPath.Trim();
        albumPath = albumPath.Trim();
        if (string.IsNullOrEmpty(artistPath) || string.IsNullOrEmpty(albumPath))
        {
            return BadRequest(new { message = "artistPath and albumPath are required" });
        }

        var userId = CurrentUserId;
        var pref = await _db.ArtistArtPreferences
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ArtistPath == artistPath);

        if (pref is null)
        {
            _db.ArtistArtPreferences.Add(new ArtistArtPreference
            {
                UserId = userId,
                ArtistPath = artistPath,
                AlbumPath = albumPath,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            pref.AlbumPath = albumPath;
            pref.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string artistPath)
    {
        artistPath = artistPath.Trim();
        if (string.IsNullOrEmpty(artistPath))
        {
            return BadRequest(new { message = "artistPath is required" });
        }

        var pref = await _db.ArtistArtPreferences
            .FirstOrDefaultAsync(a => a.UserId == CurrentUserId && a.ArtistPath == artistPath);
        if (pref is null)
        {
            return NotFound();
        }

        _db.ArtistArtPreferences.Remove(pref);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}