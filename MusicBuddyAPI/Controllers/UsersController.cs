using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicBuddyAPI.Data;
using MusicBuddyShared.Dtos;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public UsersController(MusicBuddyDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        return await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Alias = u.Alias,
                IsAdmin = u.IsAdmin,
                IsDisabled = u.IsDisabled,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto)
    {
        var username = dto.Username.Trim();
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest(new { message = "Username is required" });
        }
        if (string.IsNullOrEmpty(dto.Password))
        {
            return BadRequest(new { message = "Password is required" });
        }
        if (await _db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
        {
            return Conflict(new { message = "A user with that name already exists" });
        }

        var user = new User
        {
            Username = username,
            PasswordHash = new PasswordHasher<User>().HashPassword(null!, dto.Password),
            Alias = dto.Alias,
            IsAdmin = dto.IsAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDto(user));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }
        return ToDto(user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (dto.IsAdmin.HasValue && dto.IsAdmin.Value != user.IsAdmin)
        {
            if (!dto.IsAdmin.Value && user.IsAdmin && !await HasAnotherEnabledAdmin(id))
            {
                return BadRequest(new { message = "Cannot demote the last enabled admin account" });
            }

            user.IsAdmin = dto.IsAdmin.Value;
        }

        if (dto.IsDisabled.HasValue && dto.IsDisabled.Value != user.IsDisabled)
        {
            if (dto.IsDisabled.Value && user.IsAdmin && !await HasAnotherEnabledAdmin(id))
            {
                return BadRequest(new { message = "Cannot disable the last enabled admin account" });
            }

            user.IsDisabled = dto.IsDisabled.Value;
        }

        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = new PasswordHasher<User>().HashPassword(null!, dto.Password);
        }

        if (dto.Alias is not null)
        {
            user.Alias = dto.Alias;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsAdmin && !await HasAnotherEnabledAdmin(id))
        {
            return BadRequest(new { message = "Cannot delete the last enabled admin account" });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> HasAnotherEnabledAdmin(int excludeId)
    {
        return await _db.Users.AnyAsync(u => u.Id != excludeId && u.IsAdmin && !u.IsDisabled);
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Alias = u.Alias,
        IsAdmin = u.IsAdmin,
        IsDisabled = u.IsDisabled,
        CreatedAt = u.CreatedAt
    };
}
