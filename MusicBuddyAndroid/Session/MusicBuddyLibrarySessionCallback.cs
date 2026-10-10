using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using MusicBuddyAndroid.Api;
using IListenableFuture = Google.Common.Util.Concurrent.IListenableFuture;
using SessionError = AndroidX.Media3.Session.SessionError;

namespace MusicBuddyAndroid.Session;

public sealed class MusicBuddyLibrarySessionCallback :
    Java.Lang.Object,
    MediaLibraryService.MediaLibrarySession.ICallback
{
    private readonly MusicBuddyClient _client;
    private readonly CatalogCache _catalog;
    private readonly ArtCache _artCache;
    private readonly AlbumSlot _albumSlot;
    private int _pickGeneration;
    private string _rootPickId;

    public MusicBuddyLibrarySessionCallback(
        MusicBuddyClient client,
        CatalogCache catalog,
        ArtCache artCache,
        AlbumSlot albumSlot)
    {
        _client = client;
        _catalog = catalog;
        _artCache = artCache;
        _albumSlot = albumSlot;
    }

    // ---- Library methods ----

    public IListenableFuture OnGetLibraryRoot(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        MediaLibraryService.LibraryParams libraryParams)
    {
        var root = new MediaItem.Builder()
            .SetMediaId(BrowseIds.Root)
            .SetMediaMetadata(new MediaMetadata.Builder()
                .SetTitle("MusicBuddy")
                .SetIsBrowsable(Java.Lang.Boolean.True)
                .SetIsPlayable(Java.Lang.Boolean.False)
                .Build())
            .Build();
        return Futures.FromResult(LibraryResult.OfItem(root, libraryParams));
    }

    public IListenableFuture OnGetChildren(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string parentId,
        int page,
        int pageSize,
        MediaLibraryService.LibraryParams libraryParams)
    {
        return Futures.FromTask(async () =>
        {
            var items = await GetChildrenAsync(parentId, page, pageSize);
            return LibraryResult.OfItemList(items, libraryParams);
        });
    }

    public IListenableFuture OnGetItem(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string mediaId)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnSubscribe(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string parentId,
        MediaLibraryService.LibraryParams libraryParams)
    {
        Android.Util.Log.Info("MusicBuddy", "OnSubscribe: " + parentId);
        return Futures.FromResult(LibraryResult.OfVoid());
    }

    public IListenableFuture OnUnsubscribe(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string parentId)
    {
        Android.Util.Log.Info("MusicBuddy", "OnUnsubscribe: " + parentId);
        return Futures.FromResult(LibraryResult.OfVoid());
    }

    public IListenableFuture OnSearch(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string query,
        MediaLibraryService.LibraryParams libraryParams)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnGetSearchResult(
        MediaLibraryService.MediaLibrarySession session,
        MediaSession.ControllerInfo browser,
        string query,
        int page,
        int pageSize,
        MediaLibraryService.LibraryParams libraryParams)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    // ---- MediaSession methods ----

    public MediaSession.ConnectionResult OnConnect(MediaSession session, MediaSession.ControllerInfo controller)
    {
        Android.Util.Log.Info("MusicBuddy", "OnConnect - granting extended player commands");
        // Android Auto taps route through the legacy playFromMediaId path, which
        // dispatches as player command COMMAND_SET_MEDIA_ITEM. DefaultPlayerCommands
        // doesn't include it, so without these the session silently drops every tap.
        var playerCommands = new PlayerCommands.Builder()
            .AddAll(MediaSession.ConnectionResult.DefaultPlayerCommands)
            .Add(BasePlayer.InterfaceConsts.CommandSetMediaItem)
            .Add(BasePlayer.InterfaceConsts.CommandChangeMediaItems)
            .Build();
        // Strip the search library commands - otherwise Auto shows a search
        // icon that has nothing behind it.
        var sessionCommands = MediaSession.ConnectionResult.DefaultSessionAndLibraryCommands
            .BuildUpon()
            .Remove(SessionCommand.CommandCodeLibrarySearch)
            .Remove(SessionCommand.CommandCodeLibraryGetSearchResult)
            .Build();
        return MediaSession.ConnectionResult.Accept(sessionCommands, playerCommands);
    }

    public void OnPostConnect(MediaSession session, MediaSession.ControllerInfo controller)
    {
        Android.Util.Log.Info("MusicBuddy", "OnPostConnect - controller connected");
    }

    public void OnDisconnected(MediaSession session, MediaSession.ControllerInfo controller) { }

    public void OnPlayerInteractionFinished(
        MediaSession session,
        MediaSession.ControllerInfo controllerInfo,
        PlayerCommands playerCommands) { }

    public int OnPlayerCommandRequest(MediaSession session, MediaSession.ControllerInfo controller, int playerCommand)
    {
        // Only reached when the command passed the availability check - if this
        // never logs on a tap, the command is being dropped before our callback.
        Android.Util.Log.Info("MusicBuddy", "OnPlayerCommandRequest: " + playerCommand);
        // Must return a SessionResult code (0 = success), NOT the command number -
        // any non-zero value makes Media3 treat the command as rejected.
        return SessionResult.ResultSuccess;
    }

    public bool OnMediaButtonEvent(MediaSession session, MediaSession.ControllerInfo controllerInfo, Android.Content.Intent intent)
    {
        return false;
    }

    public IListenableFuture OnAddMediaItems(
        MediaSession mediaSession,
        MediaSession.ControllerInfo controller,
        IList<MediaItem> mediaItems)
    {
        return Futures.FromTask(async () =>
        {
            var resolved = new List<MediaItem>();
            foreach (var item in mediaItems)
            {
                var id = item.MediaId;
                if (id != null && id.StartsWith(BrowseIds.TrackPrefix, StringComparison.Ordinal))
                {
                    var trackPath = BrowseIds.Unwrap(id, BrowseIds.TrackPrefix);
                    resolved.Add(ResolveTrackItemOffline(trackPath));
                }
                else
                {
                    resolved.Add(item);
                }
            }
            return ToJavaList(resolved);
        });
    }

    public IListenableFuture OnSetMediaItems(
        MediaSession mediaSession,
        MediaSession.ControllerInfo controller,
        IList<MediaItem> mediaItems,
        int startIndex,
        long startPositionMs)
    {
        var resolved = new List<MediaItem>();
        int? expansionIndex = null;
        foreach (var item in mediaItems)
        {
            var id = item.MediaId;
            Android.Util.Log.Info("MusicBuddy", "OnSetMediaItems: " + (id ?? "<null>"));
            if (id == null)
            {
                resolved.Add(item);
            }
            else if (id.StartsWith(BrowseIds.TrackPrefix, StringComparison.Ordinal))
            {
                // Android Auto taps use the legacy playFromMediaId path: the host
                // sends a fresh MediaItem with only the mediaId (no URI/metadata).
                // Expand a tapped album track into the full album queue so
                // next/previous work and the album doesn't loop as a 1-item queue.
                // The browse-built items carry embedded art, which the offline
                // single-item resolve can't (it only has the tapped track's path).
                var trackPath = BrowseIds.Unwrap(id, BrowseIds.TrackPrefix);
                if (mediaItems.Count == 1 &&
                    _albumSlot.TryGetForTrack(trackPath, out var queue, out var index))
                {
                    resolved.AddRange(queue);
                    expansionIndex = index;
                }
                else
                {
                    resolved.Add(ResolveTrackItemOffline(trackPath));
                }
            }
            else
            {
                resolved.Add(item);
            }
        }
        // Media3 1.10: onSetMediaItems must complete with MediaItemsWithStartPosition -
        // the legacy playFromMediaId path casts the future's result directly.
        var startIdx = expansionIndex ?? (startIndex >= 0 && startIndex < resolved.Count ? startIndex : 0);
        var startPos = startPositionMs >= 0 ? startPositionMs : 0;
        return Futures.FromResult(new MediaSession.MediaItemsWithStartPosition(resolved, startIdx, startPos));
    }

    public IListenableFuture OnCustomCommand(
        MediaSession session,
        MediaSession.ControllerInfo controller,
        SessionCommand customCommand,
        Android.OS.Bundle args)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnCustomCommand(
        MediaSession session,
        MediaSession.ControllerInfo controller,
        SessionCommand customCommand,
        Android.OS.Bundle args,
        MediaSession.IProgressReporter progressReporter)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnPlaybackResumption(MediaSession mediaSession, MediaSession.ControllerInfo controller)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnPlaybackResumption(
        MediaSession mediaSession,
        MediaSession.ControllerInfo controller,
        bool isForPlayback)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnSetRating(MediaSession session, MediaSession.ControllerInfo controller, Rating rating)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    public IListenableFuture OnSetRating(MediaSession session, MediaSession.ControllerInfo controller, string mediaId, Rating rating)
    {
        return Futures.FromResult(LibraryResult.OfError(SessionError.ErrorNotSupported));
    }

    // ---- Track resolution (playFromMediaId) ----

    // Builds a playable item straight from the mediaId - no catalog/album fetches on
    // the tap path (AA times out waiting if we do network work here). Title comes from
    // the filename; art comes from whatever the cache already has from browsing.
    private MediaItem ResolveTrackItemOffline(string trackPath)
    {
        var title = Uri.UnescapeDataString(trackPath[(trackPath.LastIndexOf('/') + 1)..]);
        if (title.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            title = title[..^4];

        var metadata = new MediaMetadata.Builder()
            .SetTitle(title)
            .SetIsBrowsable(Java.Lang.Boolean.False)
            .SetIsPlayable(Java.Lang.Boolean.True);

        if (_artCache.TryGetCached(trackPath, out var artBytes) && artBytes != null)
            metadata.SetArtworkData(artBytes, new Java.Lang.Integer(0));

        var streamUrl = _client.ToStreamUrl(trackPath);
        Android.Util.Log.Info("MusicBuddy", "Resolved offline: " + trackPath + " -> " + streamUrl);
        return new MediaItem.Builder()
            .SetMediaId(BrowseIds.Track(trackPath))
            .SetUri(streamUrl)
            .SetMediaMetadata(metadata.Build())
            .Build();
    }

    // ---- Browse tree ----

    private async Task<IList<MediaItem>> GetChildrenAsync(string mediaId, int page, int pageSize)
    {
        if (mediaId == BrowseIds.Root)
        {
            // The Random Album row carries a generation id: AA caches children
            // per mediaId, so a never-seen id is the only kind of row that
            // direct-loads fresh content on tap (see the picks branch).
            _rootPickId = BrowseIds.PickPrefix + Interlocked.Increment(ref _pickGeneration);
            return Page(new[]
            {
                Browsable(BrowseIds.Artists, "Artists"),
                Browsable(_rootPickId, "Random Album")
            }, page, pageSize);
        }

        if (mediaId == BrowseIds.Artists)
        {
            var catalog = await _catalog.GetAsync(_client);
            var artists = catalog.Artists
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => Browsable(BrowseIds.Artist(a.Name), a.Name))
                .ToList();
            return Page(artists, page, pageSize);
        }

        if (mediaId.StartsWith(BrowseIds.ArtistPrefix, StringComparison.Ordinal))
        {
            var artistName = BrowseIds.Unwrap(mediaId, BrowseIds.ArtistPrefix);
            var catalog = await _catalog.GetAsync(_client);
            var artist = catalog.Artists.FirstOrDefault(a =>
                string.Equals(a.Name, artistName, StringComparison.OrdinalIgnoreCase));
            if (artist == null) return Array.Empty<MediaItem>();

            var sorted = artist.Albums
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var pageAlbums = PageDtos(sorted, page, pageSize);

            var artTasks = pageAlbums
                .Select(a => string.IsNullOrEmpty(a.FirstTrackPath)
                    ? Task.FromResult(new ArtResult(null, true))
                    : _artCache.GetAsync(_client, a.FirstTrackPath))
                .ToArray();
            var arts = await Task.WhenAll(artTasks);

            var albums = pageAlbums
                .Select((a, i) => AlbumItem(a, artist.Name, arts[i]))
                .ToList();
            return albums;
        }

        if (mediaId.StartsWith(BrowseIds.AlbumPrefix, StringComparison.Ordinal))
        {
            var albumPath = BrowseIds.Unwrap(mediaId, BrowseIds.AlbumPrefix);
            var album = await _client.GetAlbumAsync(albumPath);
            var artistName = album.AlbumArtist
                             ?? (albumPath.Contains('/') ? Uri.UnescapeDataString(albumPath[..albumPath.LastIndexOf('/')]) : albumPath);

            var art = album.Tracks.Count > 0
                ? await _artCache.GetAsync(_client, album.Tracks[0].Path)
                : new ArtResult(null, true);

            // Only track 1's path was actually requested above; the tap path
            // resolves by the tapped track and can't fetch (AA times out), so
            // prime every track path with the album's cover.
            if (art.Bytes != null)
            {
                foreach (var t in album.Tracks)
                    _artCache.Prime(t.Path, art.Bytes);
            }

            var tracks = album.Tracks
                .Select(t => TrackItem(t, artistName, album.Name, art))
                .ToList();
            // Cache the whole album so tapping any track can queue the full album.
            _albumSlot.Remember(tracks);
            return Page(tracks, page, pageSize);
        }

        if (mediaId.StartsWith(BrowseIds.PickPrefix, StringComparison.Ordinal))
        {
            Android.Util.Log.Info("MusicBuddy", "Serving random picks for " + mediaId);
            // Every picks screen is just a "Roll the dice!" button pointing at
            // the next generation id; result screens (everything except the
            // entry one from the home row) also list five random albums. AA
            // caches screen nodes, but it loads a never-seen child id directly
            // on tap (same path album taps use), so each roll re-requests and
            // gets five fresh picks.
            var rollRow = Browsable(
                BrowseIds.PickPrefix + Interlocked.Increment(ref _pickGeneration), "Roll the dice!");
            if (mediaId == _rootPickId)
                return Page(new[] { rollRow }, page, pageSize);

            var catalog = await _catalog.GetAsync(_client);
            var albums = catalog.Artists
                .Where(a => !a.IsUncatalogued)
                .SelectMany(a => a.Albums
                    .Where(al => !string.IsNullOrEmpty(al.Path) && al.TrackCount > 0)
                    .Select(al => (Artist: a.Name, Album: al)))
                .ToList();
            if (albums.Count == 0) return Page(new[] { rollRow }, page, pageSize);

            var picks = albums.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
            var artTasks = picks
                .Select(p => string.IsNullOrEmpty(p.Album.FirstTrackPath)
                    ? Task.FromResult(new ArtResult(null, true))
                    : _artCache.GetAsync(_client, p.Album.FirstTrackPath))
                .ToArray();
            var arts = await Task.WhenAll(artTasks);

            var items = new List<MediaItem> { rollRow };
            items.AddRange(picks.Select((p, i) => AlbumItem(p.Album, p.Artist, arts[i])));
            return Page(items, page, pageSize);
        }

        return Array.Empty<MediaItem>();
    }

    private MediaItem Browsable(string mediaId, string title)
    {
        return new MediaItem.Builder()
            .SetMediaId(mediaId)
            .SetMediaMetadata(new MediaMetadata.Builder()
                .SetTitle(title)
                .SetIsBrowsable(Java.Lang.Boolean.True)
                .SetIsPlayable(Java.Lang.Boolean.False)
                .Build())
            .Build();
    }

    private MediaItem AlbumItem(AlbumDto album, string artistName, ArtResult art)
    {
        var title = string.IsNullOrEmpty(album.Name) ? "Unknown Album" : album.Name;
        var metadata = new MediaMetadata.Builder()
            .SetTitle(title)
            .SetArtist(artistName)
            .SetAlbumTitle(title)
            .SetIsBrowsable(Java.Lang.Boolean.True)
            .SetIsPlayable(Java.Lang.Boolean.False);

        ApplyArtwork(metadata, art);

        return new MediaItem.Builder()
            .SetMediaId(BrowseIds.Album(album.Path))
            .SetMediaMetadata(metadata.Build())
            .Build();
    }

    private MediaItem TrackItem(TrackDto track, string artistName, string albumTitle, ArtResult art)
    {
        var metadata = new MediaMetadata.Builder()
            .SetTitle(track.DisplayTitle)
            .SetArtist(track.Artist ?? artistName)
            .SetAlbumTitle(albumTitle)
            .SetIsBrowsable(Java.Lang.Boolean.False)
            .SetIsPlayable(Java.Lang.Boolean.True);

        ApplyArtwork(metadata, art);

        return new MediaItem.Builder()
            .SetMediaId(BrowseIds.Track(track.Path))
            .SetUri(_client.ToStreamUrl(track.Path))
            .SetMediaMetadata(metadata.Build())
            .Build();
    }

    // Android Auto fetches artUri itself without our session cookie (it would 401),
    // so embedded bytes are the only artwork path that works on the car display.
    // Type 0 = auto-detect (PNG/JPEG); the single-arg overload is obsolete in this binding.
    private static void ApplyArtwork(MediaMetadata.Builder metadata, ArtResult art)
    {
        if (art.Bytes != null)
            metadata.SetArtworkData(art.Bytes, new Java.Lang.Integer(0));
    }

    private static IList<MediaItem> Page(IEnumerable<MediaItem> items, int page, int pageSize)
    {
        var list = items as IList<MediaItem> ?? items.ToList();
        if (page < 0) page = 0;
        if (pageSize <= 0) pageSize = 100;
        return list.Skip(page * pageSize).Take(pageSize).ToList();
    }

    private static List<AlbumDto> PageDtos(List<AlbumDto> items, int page, int pageSize)
    {
        if (page < 0) page = 0;
        if (pageSize <= 0) pageSize = 100;
        return items.Skip(page * pageSize).Take(pageSize).ToList();
    }

    private static Java.Util.ArrayList ToJavaList(List<MediaItem> items)
    {
        var javaList = new Java.Util.ArrayList();
        foreach (var item in items)
            javaList.Add(item);
        return javaList;
    }
}
