namespace MusicBuddyShared.Dtos;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
