using Android.App;
using Android.Content;
using Android.Runtime;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.Session;
using MusicBuddyAndroid.Api;
using MusicBuddyAndroid.Playback;
using MusicBuddyAndroid.Security;
using MusicBuddyAndroid.Session;

namespace MusicBuddyAndroid;

[Register("com.musicbuddy.android.MusicBuddyMediaService")]
public class MusicBuddyMediaService : MediaLibraryService
{
    private MediaLibrarySession _session;
    private IExoPlayer _player;
    private MusicBuddyClient _client;
    private CatalogCache _catalog;
    private CookieDataSourceFactory _cookieFactory;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private volatile bool _hasAuth;

    public override void OnCreate()
    {
        base.OnCreate();
        Android.Util.Log.Info("MusicBuddy", "Media service OnCreate");

        _client = new MusicBuddyClient();
        _catalog = new CatalogCache();
        _cookieFactory = new CookieDataSourceFactory(() => _client?.CookieHeader);

        var audioAttrs = new AudioAttributes.Builder()
            .SetUsage(C.UsageMedia)
            .SetContentType(C.AudioContentTypeMusic)
            .Build();

        _player = new ExoPlayerBuilder(this)
            .SetAudioAttributes(audioAttrs, true)
            .SetMediaSourceFactory(new DefaultMediaSourceFactory(_cookieFactory))
            .SetWakeMode(C.WakeModeLocal)
            .Build();

        var artCache = new ArtCache();
        var callback = new MusicBuddyLibrarySessionCallback(_client, _catalog, artCache, new AlbumSlot());
        var bitmapLoader = new CookieBitmapLoader(_client);

        var sessionBuilder = new MediaLibraryService.MediaLibrarySession.Builder(this, _player, callback);
        sessionBuilder.SetBitmapLoaderAsMediaSessionBuilder(bitmapLoader);
        // AA re-renders the queue on every periodic position update (~1/s),
        // snapping scroll back to the now-playing item (androidx/media#2192).
        sessionBuilder.SetPeriodicPositionUpdateEnabledBuilder(false);
        _session = sessionBuilder.Build();

        _ = Task.Run(() => EnsureInitAsync(CancellationToken.None));
    }

    public override MediaLibrarySession OnGetSession(MediaSession.ControllerInfo controllerInfo)
        => _session;

    public override MediaLibrarySession OnGetSessionFromMediaLibraryService(MediaSession.ControllerInfo controllerInfo)
        => _session;

    // Returns true when the client is logged in and the catalog is reachable.
    internal async Task<bool> EnsureInitAsync(CancellationToken ct)
    {
        if (_initialized) return _hasAuth;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return _hasAuth;

            var creds = CredentialStore.Load(this);
            if (creds != null && _client != null && _catalog != null)
            {
                try
                {
                    _client.Configure(creds.Value.ServerUrl, creds.Value.Username, creds.Value.Password);
                    await _client.LoginAsync(ct);
                    await _catalog.GetAsync(_client, ct); // warm the catalog
                    _hasAuth = true;
                    Android.Util.Log.Info("MusicBuddy", "Media service ready (user " + creds.Value.Username + ")");
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("MusicBuddy", "Media service init failed: " + ex);
                    _hasAuth = false;
                }
            }

            _initialized = true;
            return _hasAuth;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public override void OnDestroy()
    {
        _session?.Release();
        _player?.Release();
        _client?.Dispose();
        _session = null;
        _player = null;
        _client = null;
        base.OnDestroy();
    }
}
