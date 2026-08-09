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
public class PlaylistsController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public PlaylistsController(MusicBuddyDbContext db) => _db = db;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<Playlist>>> GetAll([FromQuery] string? type = null)
    {
        var query = _db.Playlists.Where(p => p.UserId == CurrentUserId);
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(p => p.FileType == type);
        }
        return await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlaylistDetail>> Get(int id)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        var tracks = await _db.PlaylistTracks
            .Where(t => t.PlaylistId == id)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();

        return Ok(new PlaylistDetail
        {
            Id = playlist.Id,
            Name = playlist.Name,
            FileType = playlist.FileType,
            CreatedAt = playlist.CreatedAt,
            UpdatedAt = playlist.UpdatedAt,
            Tracks = tracks.Select(t => new PlaylistTrackDto
            {
                Id = t.Id,
                FilePath = t.FilePath,
                FileName = t.FileName,
                FileSize = t.FileSize,
                SortOrder = t.SortOrder
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<Playlist>> Create(CreatePlaylistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        if (request.FileType != "mp3" && request.FileType != "sid")
            return BadRequest(new { message = "FileType must be 'mp3' or 'sid'" });

        var playlist = new Playlist
        {
            UserId = CurrentUserId,
            Name = request.Name.Trim(),
            FileType = request.FileType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = playlist.Id }, playlist);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdatePlaylistRequest request)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            playlist.Name = request.Name.Trim();

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/tracks")]
    public async Task<IActionResult> AddTracks(int id, List<AddTrackRequest> tracks)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        var maxOrder = await _db.PlaylistTracks
            .Where(t => t.PlaylistId == id)
            .MaxAsync(t => (int?)t.SortOrder) ?? 0;

        var newTracks = tracks.Select((t, i) => new PlaylistTrack
        {
            PlaylistId = id,
            FilePath = t.FilePath,
            FileName = t.FileName,
            FileSize = t.Size,
            SortOrder = maxOrder + i + 1
        }).ToList();

        _db.PlaylistTracks.AddRange(newTracks);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(newTracks.Select(t => new PlaylistTrackDto
        {
            Id = t.Id,
            FilePath = t.FilePath,
            FileName = t.FileName,
            FileSize = t.FileSize,
            SortOrder = t.SortOrder
        }).ToList());
    }

    [HttpPut("{id}/tracks/reorder")]
    public async Task<IActionResult> ReorderTracks(int id, List<ReorderTrackRequest> reorder)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        foreach (var item in reorder)
        {
            var track = await _db.PlaylistTracks
                .FirstOrDefaultAsync(t => t.Id == item.Id && t.PlaylistId == id);
            if (track is not null)
            {
                track.SortOrder = item.SortOrder;
            }
        }

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/tracks/{trackId}")]
    public async Task<IActionResult> RemoveTrack(int id, int trackId)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        var track = await _db.PlaylistTracks
            .FirstOrDefaultAsync(t => t.Id == trackId && t.PlaylistId == id);
        if (track is null) return NotFound();

        _db.PlaylistTracks.Remove(track);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/tracks")]
    public async Task<IActionResult> ClearTracks(int id)
    {
        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == CurrentUserId);
        if (playlist is null) return NotFound();

        var tracks = await _db.PlaylistTracks
            .Where(t => t.PlaylistId == id)
            .ToListAsync();

        _db.PlaylistTracks.RemoveRange(tracks);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class PlaylistDetail
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PlaylistTrackDto> Tracks { get; set; } = new();
}

public class PlaylistTrackDto
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
}

public class CreatePlaylistRequest
{
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = "mp3";
}

public class UpdatePlaylistRequest
{
    public string? Name { get; set; }
}

public class AddTrackRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class ReorderTrackRequest
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}
