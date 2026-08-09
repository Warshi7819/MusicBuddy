using System.Security.Claims;
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
public class ThemesController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public ThemesController(MusicBuddyDbContext db) => _db = db;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<ThemeDto>>> GetAll()
    {
        var userId = CurrentUserId;
        return await _db.Themes
            .Where(t => t.IsBuiltIn || t.UserId == userId)
            .OrderBy(t => t.Id)
            .Select(t => new ThemeDto
            {
                Id = t.Id,
                Name = t.Name,
                IsBuiltIn = t.IsBuiltIn,
                BodyBg = t.BodyBg,
                BodyColor = t.BodyColor,
                CardBg = t.CardBg,
                CardBorderColor = t.CardBorderColor,
                PrimaryColor = t.PrimaryColor,
                NavbarBg = t.NavbarBg,
                NavbarTextColor = t.NavbarTextColor,
                FooterBg = t.FooterBg,
                MutedColor = t.MutedColor
            })
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ThemeDto>> Get(int id)
    {
        var theme = await _db.Themes
            .FirstOrDefaultAsync(t => t.Id == id && (t.IsBuiltIn || t.UserId == CurrentUserId));
        if (theme is null)
        {
            return NotFound();
        }

        return ToDto(theme);
    }

    [HttpPost]
    public async Task<ActionResult<ThemeDto>> Create(ThemeDto dto)
    {
        var theme = new Theme
        {
            UserId = CurrentUserId,
            Name = dto.Name,
            BodyBg = dto.BodyBg,
            BodyColor = dto.BodyColor,
            CardBg = dto.CardBg,
            CardBorderColor = dto.CardBorderColor,
            PrimaryColor = dto.PrimaryColor,
            NavbarBg = dto.NavbarBg,
            NavbarTextColor = dto.NavbarTextColor,
            FooterBg = dto.FooterBg,
            MutedColor = dto.MutedColor
        };
        _db.Themes.Add(theme);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = theme.Id }, ToDto(theme));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ThemeDto dto)
    {
        var existing = await _db.Themes
            .FirstOrDefaultAsync(t => t.Id == id && (t.IsBuiltIn || t.UserId == CurrentUserId));
        if (existing is null)
        {
            return NotFound();
        }

        if (existing.IsBuiltIn)
        {
            return BadRequest("Cannot modify built-in themes");
        }

        existing.Name = dto.Name;
        existing.BodyBg = dto.BodyBg;
        existing.BodyColor = dto.BodyColor;
        existing.CardBg = dto.CardBg;
        existing.CardBorderColor = dto.CardBorderColor;
        existing.PrimaryColor = dto.PrimaryColor;
        existing.NavbarBg = dto.NavbarBg;
        existing.NavbarTextColor = dto.NavbarTextColor;
        existing.FooterBg = dto.FooterBg;
        existing.MutedColor = dto.MutedColor;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await _db.Themes
            .FirstOrDefaultAsync(t => t.Id == id && (t.IsBuiltIn || t.UserId == CurrentUserId));
        if (existing is null)
        {
            return NotFound();
        }

        if (existing.IsBuiltIn)
        {
            return BadRequest("Cannot delete built-in themes");
        }

        _db.Themes.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ThemeDto ToDto(Theme t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        IsBuiltIn = t.IsBuiltIn,
        BodyBg = t.BodyBg,
        BodyColor = t.BodyColor,
        CardBg = t.CardBg,
        CardBorderColor = t.CardBorderColor,
        PrimaryColor = t.PrimaryColor,
        NavbarBg = t.NavbarBg,
        NavbarTextColor = t.NavbarTextColor,
        FooterBg = t.FooterBg,
        MutedColor = t.MutedColor
    };
}
