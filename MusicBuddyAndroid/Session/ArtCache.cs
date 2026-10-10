using System.Collections.Concurrent;
using MusicBuddyAndroid.Api;

namespace MusicBuddyAndroid.Session;

public readonly struct ArtResult
{
    public ArtResult(byte[] bytes, bool noArtExists)
    {
        Bytes = bytes;
        NoArtExists = noArtExists;
    }

    // Real cover bytes; null when none available.
    public byte[] Bytes { get; }
    // True when the server answered with its 1x1 placeholder (no art exists).
    public bool NoArtExists { get; }
}

// Fetches album art through the authenticated client and caches it in memory.
// The server returns a 1x1 PNG placeholder when an album has no cover - we
// detect that so items don't carry a useless black square.
public sealed class ArtCache
{
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Synchronous lookup for already-fetched art (tap path must not block on network).
    public bool TryGetCached(string trackPath, out byte[] bytes)
    {
        if (!string.IsNullOrEmpty(trackPath) && _cache.TryGetValue(trackPath, out var found))
        {
            bytes = found;
            return true;
        }
        bytes = null;
        return false;
    }

    // Stores already-fetched art under an extra key so lookups by any track
    // path hit (the tap path resolves by the tapped track, but only the
    // album's first track path is ever actually requested).
    public void Prime(string trackPath, byte[] bytes)
    {
        if (!string.IsNullOrEmpty(trackPath) && bytes != null && bytes.Length > 0)
            _cache.TryAdd(trackPath, bytes);
    }

    public async Task<ArtResult> GetAsync(MusicBuddyClient client, string trackPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(trackPath))
            return new ArtResult(null, true);

        if (_cache.TryGetValue(trackPath, out var cached))
            return new ArtResult(cached, false);

        try
        {
            var url = client.ToAbsoluteUrl("/api/albumart?path=" + Uri.EscapeDataString(trackPath));
            var bytes = await client.GetByteArrayAsync(url, ct);
            if (bytes == null || bytes.Length == 0)
                return new ArtResult(null, false);

            if (IsOnePixelPlaceholder(bytes))
                return new ArtResult(null, true);

            _cache[trackPath] = bytes;
            return new ArtResult(bytes, false);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("MusicBuddy", "Art fetch failed for " + trackPath + ": " + ex.Message);
            return new ArtResult(null, false);
        }
    }

    // The web API returns a 1x1 PNG when no art exists; detect via PNG IHDR chunk.
    private static bool IsOnePixelPlaceholder(byte[] bytes)
    {
        if (bytes.Length < 24) return false;
        // PNG signature
        if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
            return false;
        // Width/height are big-endian at offsets 16 and 20 (after signature + IHDR header)
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width == 1 && height == 1;
    }
}
