namespace MusicBuddyShared.Models;

public class ArtistArtPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ArtistPath { get; set; } = string.Empty;
    public string AlbumPath { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}