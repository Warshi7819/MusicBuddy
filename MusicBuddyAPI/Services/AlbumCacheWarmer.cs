using Microsoft.Extensions.DependencyInjection;
using MusicBuddyAPI.Services;

namespace MusicBuddyAPI.Services;

public class AlbumCacheWarmer : BackgroundService
{
    private readonly AlbumCatalogService _catalog;
    private readonly RandomTrackService _randomTrack;
    private readonly ArtistArtPrewarmer _artPrewarmer;
    private readonly ILogger<AlbumCacheWarmer> _logger;

    public AlbumCacheWarmer(
        AlbumCatalogService catalog,
        RandomTrackService randomTrack,
        ArtistArtPrewarmer artPrewarmer,
        ILogger<AlbumCacheWarmer> logger)
    {
        _catalog = catalog;
        _randomTrack = randomTrack;
        _artPrewarmer = artPrewarmer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _catalog.WarmAsync();
            await _randomTrack.WarmAsync();
            await _artPrewarmer.PrewarmAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Album catalog warm-up failed");
        }
    }
}