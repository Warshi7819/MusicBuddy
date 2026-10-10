using System.Text.Json.Serialization;

namespace MusicBuddyAndroid.Api;

public class AlbumCatalogPayloadDto
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

public class AlbumDetailDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string AlbumArtist { get; set; }
    public uint? Year { get; set; }
    public string Genre { get; set; }
    public List<TrackDto> Tracks { get; set; } = new();
}

public class TrackDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title!;
}
