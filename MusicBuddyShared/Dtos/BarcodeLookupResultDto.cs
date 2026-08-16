namespace MusicBuddyShared.Dtos;

public class BarcodeLookupResultDto
{
    public bool Found { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public CollectionMatchResult? Match { get; set; }
}

public class CollectionMatchResult
{
    public bool Found { get; set; }
    public bool ExactAlbum { get; set; }
    public string? ArtistPath { get; set; }
    public string? AlbumPath { get; set; }
    public string? AlbumLink { get; set; }
    public string? ArtistLink { get; set; }
}
