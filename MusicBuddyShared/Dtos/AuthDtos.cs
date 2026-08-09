namespace MusicBuddyShared.Dtos;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsDisabled { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
