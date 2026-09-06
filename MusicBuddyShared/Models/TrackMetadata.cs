namespace MusicBuddyShared.Models;

public class TrackMetadata
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string UrlPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? TrackName { get; set; }
    public string? Artist { get; set; }
    public string? AlbumArtist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int DurationSeconds { get; set; }
    public int ChannelCount { get; set; } = 2;
    public string ArtistFolder { get; set; } = string.Empty;
    public DateTime LastScanned { get; set; } = DateTime.UtcNow;
}
