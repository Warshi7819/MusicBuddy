namespace MusicBuddyAPI.Services;

public class AlbumCacheWarmer : BackgroundService
{
    private readonly AlbumCatalogService _catalog;
    private readonly RandomTrackService _randomTrack;
    private readonly ILogger<AlbumCacheWarmer> _logger;

    public AlbumCacheWarmer(
        AlbumCatalogService catalog,
        RandomTrackService randomTrack,
        ILogger<AlbumCacheWarmer> logger)
    {
        _catalog = catalog;
        _randomTrack = randomTrack;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _catalog.WarmAsync();
            await _randomTrack.WarmAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Album catalog warm-up failed");
        }
    }
}