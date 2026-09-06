namespace MusicBuddyWeb.Pages;

public class CatalogData
{
    public List<ArtistDto> Artists { get; set; } = new();
}

public class ArtistDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsUncatalogued { get; set; }
    public int AlbumCount { get; set; }
    public List<AlbumDto> Albums { get; set; } = new();
}

public class AlbumDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public string FirstTrackPath { get; set; } = string.Empty;
}
