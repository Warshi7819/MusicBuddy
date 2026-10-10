using System.Diagnostics;
using MusicBuddyAndroid.Api;

namespace MusicBuddyAndroid.Session;

public sealed class CatalogCache
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AlbumCatalogPayloadDto _data;
    private DateTime _fetchedAt = DateTime.MinValue;

    public async Task<AlbumCatalogPayloadDto> GetAsync(MusicBuddyClient client, CancellationToken ct = default)
    {
        if (_data != null && DateTime.UtcNow - _fetchedAt < TimeSpan.FromMinutes(10))
            return _data;

        await _lock.WaitAsync(ct);
        try
        {
            if (_data != null && DateTime.UtcNow - _fetchedAt < TimeSpan.FromMinutes(10))
                return _data;

            Debug.WriteLine("MusicBuddy: fetching album catalog");
            _data = await client.GetCatalogAsync(ct);
            _fetchedAt = DateTime.UtcNow;
            return _data;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _data = null;
        _fetchedAt = DateTime.MinValue;
    }
}
