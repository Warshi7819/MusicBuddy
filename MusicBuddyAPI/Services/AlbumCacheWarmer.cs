namespace MusicBuddyAPI.Services;

public class AlbumCacheWarmer : BackgroundService
{
    private readonly AlbumCatalogService _catalog;
    private readonly ILogger<AlbumCacheWarmer> _logger;

    public AlbumCacheWarmer(
        AlbumCatalogService catalog,
        ILogger<AlbumCacheWarmer> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _catalog.WarmAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Album catalog warm-up failed");
        }
    }
}