using System.Security.Claims;
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
public class AuthController : ControllerBase
{
    private readonly MusicBuddyDbContext _db;

    public AuthController(MusicBuddyDbContext db) => _db = db;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        var result = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        if (user.IsDisabled)
        {
            return StatusCode(403, new { message = "This account has been disabled" });
        }

        return new LoginResponse
        {
            Id = user.Id,
            Username = user.Username,
            Alias = user.Alias,
            IsAdmin = user.IsAdmin,
            IsDisabled = user.IsDisabled
        };
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var result = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect" });
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { message = "New password is required" });
        }

        user.PasswordHash = new PasswordHasher<User>().HashPassword(null!, request.NewPassword);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> GetMe()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return new LoginResponse
        {
            Id = user.Id,
            Username = user.Username,
            Alias = user.Alias,
            IsAdmin = user.IsAdmin,
            IsDisabled = user.IsDisabled
        };
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (dto.Alias is not null)
        {
            user.Alias = dto.Alias;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
