using MusicBuddyAndroid.Api;

namespace MusicBuddyAndroid.Session;

public static class BrowseIds
{
    public const string Root = "root";
    public const string Artists = "artists";
    public const string PickPrefix = "pick:";

    public const string ArtistPrefix = "artist:";
    public const string AlbumPrefix = "album:";
    public const string TrackPrefix = "track:";

    public static string Artist(string name) => ArtistPrefix + Uri.EscapeDataString(name);
    public static string Album(string path) => AlbumPrefix + Uri.EscapeDataString(path);
    public static string Track(string path) => TrackPrefix + Uri.EscapeDataString(path);

    public static string Unwrap(string mediaId, string prefix)
        => mediaId.StartsWith(prefix, StringComparison.Ordinal)
            ? Uri.UnescapeDataString(mediaId[prefix.Length..])
            : null;
}
