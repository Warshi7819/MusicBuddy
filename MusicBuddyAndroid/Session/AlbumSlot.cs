using AndroidX.Media3.Common;

namespace MusicBuddyAndroid.Session;

// Single-slot cache of the album currently being browsed. Android Auto taps
// (legacy playFromMediaId) discard the browse-list metadata and send back only
// the mediaId, so a tapped track is expanded into its full album queue from
// here instead of re-fetching over the network (AA times out on slow taps).
// One slot is enough: you can only tap a track in the album you just browsed,
// and each album browse replaces the slot.
public sealed class AlbumSlot
{
    private readonly object _gate = new();
    private List<MediaItem> _queue = new();
    private Dictionary<string, int> _indexByTrackPath = new(StringComparer.OrdinalIgnoreCase);

    public void Remember(List<MediaItem> queue)
    {
        var indexByTrackPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < queue.Count; i++)
        {
            var id = queue[i].MediaId;
            if (id != null && id.StartsWith(BrowseIds.TrackPrefix, StringComparison.Ordinal))
                indexByTrackPath[BrowseIds.Unwrap(id, BrowseIds.TrackPrefix)] = i;
        }

        lock (_gate)
        {
            _queue = queue;
            _indexByTrackPath = indexByTrackPath;
        }
    }

    // Returns the full album queue and the tapped track's index within it.
    public bool TryGetForTrack(string trackPath, out List<MediaItem> queue, out int index)
    {
        lock (_gate)
        {
            if (_indexByTrackPath.TryGetValue(trackPath, out index))
            {
                queue = _queue;
                return true;
            }
        }
        queue = null;
        index = 0;
        return false;
    }
}
