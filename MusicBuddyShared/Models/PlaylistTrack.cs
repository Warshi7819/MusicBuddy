namespace MusicBuddyShared.Models;

public class PlaylistTrack
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public string FilePath { get; set; } = string.Empty; // URL-relative path e.g. /Music/Sid/A/Track.sid
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int ChannelCount { get; set; } = 2;
    public int SortOrder { get; set; }
}
