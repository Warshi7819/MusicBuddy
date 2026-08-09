namespace MusicBuddyShared.Dtos;

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsAdmin { get; set; }
}

public class UpdateUserDto
{
    public string? Password { get; set; }
    public string? Alias { get; set; }
    public bool? IsAdmin { get; set; }
    public bool? IsDisabled { get; set; }
}
