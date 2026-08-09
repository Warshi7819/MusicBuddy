namespace MusicBuddyShared.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsDisabled { get; set; }
    public string? Alias { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
