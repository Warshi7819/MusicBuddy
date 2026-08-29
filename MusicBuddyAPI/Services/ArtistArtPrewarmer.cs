using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicBuddyAPI.Data;

namespace MusicBuddyAPI.Services;

public class ArtistArtPrewarmer
{
    private const int ProgressInterval = 50;

    private readonly AlbumCatalogService _catalog;
    private readonly AlbumArtExtractor _artExtractor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArtistArtPrewarmer> _logger;

    public ArtistArtPrewarmer(
        AlbumCatalogService catalog,
        AlbumArtExtractor artExtractor,
        IServiceScopeFactory scopeFactory,
        ILogger<ArtistArtPrewarmer> logger)
    {
        _catalog = catalog;
        _artExtractor = artExtractor;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PrewarmAsync(CancellationToken ct)
    {
        try
        {
            var payload = await _catalog.GetPayloadAsync();
            var artists = payload.Artists;
            if (artists.Count == 0) return;

            Dictionary<string, string> prefs = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MusicBuddyDbContext>();
                foreach (var pref in await db.ArtistArtPreferences
                    .Select(p => new { p.ArtistPath, p.AlbumPath })
                    .ToListAsync(ct))
                {
                    prefs[pref.ArtistPath] = pref.AlbumPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Album art pre-warm: could not load artist art preferences");
            }

            _logger.LogInformation("Album art pre-warm started for {Count} artists", artists.Count);
            var sw = Stopwatch.StartNew();
            int done = 0, withArt = 0, failed = 0;

            foreach (var artist in artists)
            {
                ct.ThrowIfCancellationRequested();

                var album = artist.Albums.FirstOrDefault(a =>
                        prefs.TryGetValue(artist.Path, out var chosen) &&
                        string.Equals(a.Path, chosen, StringComparison.OrdinalIgnoreCase))
                    ?? artist.Albums.FirstOrDefault();

                var firstTrack = album?.FirstTrackPath;
                if (string.IsNullOrWhiteSpace(firstTrack)) continue;

                done++;
                try
                {
                    var result = await _artExtractor.GetAsync(firstTrack, ct);
                    if (result is not null) withArt++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    failed++;
                }

                if (done % ProgressInterval == 0)
                {
                    _logger.LogInformation("Album art pre-warm progress {Done}/{Count}", done, artists.Count);
                }
            }

            sw.Stop();
            _logger.LogInformation(
                "Album art pre-warm complete: {Done}/{Count} artists (art cached for {WithArt}, failures {Failed}) in {ElapsedSec} s",
                done, artists.Count, withArt, failed, (int)sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Album art pre-warm cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Album art pre-warm failed");
        }
    }
}