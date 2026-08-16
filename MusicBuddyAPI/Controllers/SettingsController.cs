using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicBuddyAPI.Data;
using MusicBuddyShared.Dtos;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public SettingsController(MusicBuddyDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll()
    {
        return await _db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    [HttpPut("{key}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Upsert(string key, SettingDto dto)
    {
        key = key.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return BadRequest(new { message = "Setting key cannot be empty" });
        }

        var setting = await _db.Settings.FindAsync(key);
        if (setting is null)
        {
            _db.Settings.Add(new Setting { Key = key, Value = dto.Value ?? string.Empty });
        }
        else
        {
            setting.Value = dto.Value ?? string.Empty;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{key}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        if (setting is null)
        {
            return NotFound();
        }

        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
