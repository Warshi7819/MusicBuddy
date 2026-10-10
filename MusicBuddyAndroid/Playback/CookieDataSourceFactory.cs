using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Util;
using AndroidX.Media3.DataSource;
using MusicBuddyAndroid.Api;

namespace MusicBuddyAndroid.Playback;

// Builds a fresh DefaultHttpDataSource per request so every request carries the
// current session cookie (it may change after a silent re-login).
public sealed class CookieDataSourceFactory : Java.Lang.Object, IDataSourceFactory
{
    private readonly Func<string> _cookieHeaderProvider;

    public CookieDataSourceFactory(Func<string> cookieHeaderProvider)
    {
        _cookieHeaderProvider = cookieHeaderProvider;
    }

    public IDataSource CreateDataSource()
    {
        var factory = new DefaultHttpDataSource.Factory()
            .SetUserAgent("MusicBuddyAndroid")
            .SetConnectTimeoutMs(15000)
            .SetReadTimeoutMs(30000);

        var cookie = _cookieHeaderProvider();
        if (!string.IsNullOrEmpty(cookie))
        {
            factory.SetDefaultRequestProperties(new Dictionary<string, string>
            {
                ["Cookie"] = cookie
            });
        }

        return factory.CreateDataSource();
    }
}

// Album art lives behind the session cookie, so Media3's default bitmap loader
// (plain HTTP, no auth) would get 401s. This one goes through the API client.
public sealed class CookieBitmapLoader : Java.Lang.Object, IBitmapLoader
{
    private readonly MusicBuddyClient _client;

    public CookieBitmapLoader(MusicBuddyClient client)
    {
        _client = client;
    }

    public bool SupportsMimeType(string mimeType)
        => mimeType != null && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public global::Google.Common.Util.Concurrent.IListenableFuture DecodeBitmap(byte[] data)
    {
        return Futures.Create(completer =>
        {
            if (data == null || data.Length == 0)
            {
                completer.SetException(new Java.Lang.RuntimeException("Empty bitmap data"));
                return "mb-art";
            }
            var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(data, 0, data.Length);
            if (bitmap == null)
                completer.SetException(new Java.Lang.RuntimeException("Could not decode bitmap"));
            else
                completer.Set(bitmap);
            return "mb-art";
        });
    }

    public global::Google.Common.Util.Concurrent.IListenableFuture LoadBitmap(Android.Net.Uri uri)
    {
        return Futures.Create(completer =>
        {
            if (uri == null)
            {
                completer.SetException(new Java.Lang.RuntimeException("Null art URI"));
                return "mb-art";
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var bytes = await _client.GetByteArrayAsync(uri.ToString());
                    var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
                    if (bitmap == null)
                        completer.SetException(new Java.Lang.RuntimeException("Could not decode art from " + uri));
                    else
                        completer.Set(bitmap);
                }
                catch (Exception ex)
                {
                    completer.SetException(new Java.Lang.RuntimeException(ex.Message));
                }
            });
            return "mb-art";
        });
    }
}
