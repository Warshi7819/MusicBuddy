namespace MusicBuddyAPI.Services;

public class MetadataStartupWarmer : BackgroundService
{
    private readonly TrackMetadataService _metadata;
    private readonly ILogger<MetadataStartupWarmer> _logger;

    public MetadataStartupWarmer(TrackMetadataService metadata, ILogger<MetadataStartupWarmer> logger)
    {
        _metadata = metadata;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _metadata.StartRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata startup refresh failed");
        }
    }
}
