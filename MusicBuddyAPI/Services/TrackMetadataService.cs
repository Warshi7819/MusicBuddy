using Microsoft.EntityFrameworkCore;
using MusicBuddyAPI.Data;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Services;

public class TrackMetadataService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FileCacheService _fileCache;
    private readonly ILogger<TrackMetadataService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsRefreshing { get; private set; }
    public int TotalFiles { get; private set; }
    public int ScannedFiles { get; private set; }
    public DateTime? LastRefreshTime { get; private set; }
    public string? CurrentError { get; private set; }

    public TrackMetadataService(
        IServiceScopeFactory scopeFactory,
        FileCacheService fileCache,
        ILogger<TrackMetadataService> logger)
    {
        _scopeFactory = scopeFactory;
        _fileCache = fileCache;
        _logger = logger;
    }

    public Task<RefreshStatusDto> GetStatusAsync()
    {
        return Task.FromResult(new RefreshStatusDto
        {
            IsRefreshing = IsRefreshing,
            TotalFiles = TotalFiles,
            ScannedFiles = ScannedFiles,
            LastRefreshTime = LastRefreshTime,
            Error = CurrentError
        });
    }

    public async Task StartRefreshAsync()
    {
        if (IsRefreshing) return;

        await _lock.WaitAsync();
        try
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            CurrentError = null;
            TotalFiles = 0;
            ScannedFiles = 0;
        }
        finally
        {
            _lock.Release();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await DoRefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metadata refresh failed");
                CurrentError = ex.Message;
            }
            finally
            {
                IsRefreshing = false;
            }
        });
    }

    private async Task DoRefreshAsync()
    {
        var root = _fileCache.GetRootPath("mp3");
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            _logger.LogWarning("TrackMetadataService: MP3 root missing ({Root})", root);
            CurrentError = "MP3 root directory not found";
            return;
        }

        var urlPrefix = _fileCache.GetUrlPrefix("mp3");
        var now = DateTime.UtcNow;

        _logger.LogInformation("TrackMetadataService: scanning {Root}", root);

        var allFiles = await Task.Run(() =>
            Directory.EnumerateFiles(root, "*.mp3", SearchOption.AllDirectories).ToList());

        TotalFiles = allFiles.Count;
        ScannedFiles = 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicBuddyDbContext>();

        var existingPaths = await db.TrackMetadata
            .Select(t => t.FilePath)
            .ToHashSetAsync();

        var entries = new List<TrackMetadata>();
        var batch = 0;
        const int batchSize = 500;

        foreach (var filePath in allFiles)
        {
            var relativeFromRoot = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            var parts = relativeFromRoot.Split('/', 2);
            bool isLooseFile = !relativeFromRoot.Contains('/');
            string artistFolder;
            if (isLooseFile)
                artistFolder = "Uncatalogued";
            else
                artistFolder = parts[0];

            var urlPath = urlPrefix + "/" + relativeFromRoot;
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Exists ? fileInfo.Length : 0;

            string? trackName = null;
            string? artist = null;
            string? albumArtist = null;
            string? album = null;
            string? genre = null;
            int? year = null;
            int durationSeconds = 0;
            int channelCount = 2;

            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                trackName = tagFile.Tag.Title;
                artist = tagFile.Tag.FirstPerformer;
                albumArtist = tagFile.Tag.FirstAlbumArtist;
                album = tagFile.Tag.Album;
                genre = tagFile.Tag.FirstGenre;
                if (tagFile.Tag.Year > 0) year = (int)tagFile.Tag.Year;
                durationSeconds = (int)tagFile.Properties.Duration.TotalSeconds;
                channelCount = tagFile.Properties.AudioChannels;
            }
            catch
            {
                // Tag reading failed, use defaults
            }

            if (string.IsNullOrWhiteSpace(trackName))
                trackName = Path.GetFileNameWithoutExtension(filePath);

            entries.Add(new TrackMetadata
            {
                FilePath = filePath,
                UrlPath = urlPath,
                FileName = Path.GetFileName(filePath),
                FileSize = fileSize,
                TrackName = trackName,
                Artist = artist,
                AlbumArtist = albumArtist,
                Album = album,
                Genre = genre,
                Year = year,
                DurationSeconds = durationSeconds,
                ChannelCount = channelCount,
                ArtistFolder = artistFolder,
                LastScanned = now
            });

            batch++;
            ScannedFiles++;

            if (batch >= batchSize)
            {
                await UpsertBatchAsync(db, entries, existingPaths);
                entries.Clear();
                batch = 0;
            }
        }

        if (entries.Count > 0)
            await UpsertBatchAsync(db, entries, existingPaths);

        // Remove entries that were not scanned (deleted files)
        var scannedPaths = await db.TrackMetadata
            .Where(t => t.LastScanned < now)
            .Select(t => t.FilePath)
            .ToListAsync();

        if (scannedPaths.Count > 0)
        {
            db.TrackMetadata.RemoveRange(
                db.TrackMetadata.Where(t => scannedPaths.Contains(t.FilePath)));
            await db.SaveChangesAsync();
        }

        LastRefreshTime = now;
        _logger.LogInformation(
            "TrackMetadataService: refresh complete — {Total} files scanned", TotalFiles);
    }

    private static async Task UpsertBatchAsync(MusicBuddyDbContext db, List<TrackMetadata> entries, HashSet<string> existingPaths)
    {
        var batchPaths = entries.Select(e => e.FilePath).ToList();
        var existingIds = await db.TrackMetadata
            .Where(t => batchPaths.Contains(t.FilePath))
            .Select(t => new { t.FilePath, t.Id })
            .ToDictionaryAsync(t => t.FilePath, t => t.Id);

        foreach (var entry in entries)
        {
            if (existingIds.TryGetValue(entry.FilePath, out var existingId))
            {
                entry.Id = existingId;
                db.TrackMetadata.Update(entry);
            }
            else
            {
                db.TrackMetadata.Add(entry);
                existingPaths.Add(entry.FilePath);
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<List<TrackMetadata>> SearchAsync(string? song = null, string? artist = null, string? genre = null, int maxResults = 100)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicBuddyDbContext>();

        IQueryable<TrackMetadata> query = db.TrackMetadata;

        if (!string.IsNullOrWhiteSpace(song))
        {
            var terms = song.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(t =>
                terms.All(term =>
                    (t.TrackName != null && t.TrackName.ToLower().Contains(term)) ||
                    t.FileName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(artist))
        {
            var terms = artist.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(t =>
                terms.All(term =>
                    t.Artist != null && t.Artist.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            var terms = genre.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(t =>
                terms.All(term =>
                    t.Genre != null && t.Genre.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(t => t.ArtistFolder)
            .ThenBy(t => t.FileName)
            .Take(maxResults)
            .ToListAsync();
    }

    public async Task<int> GetTrackCountAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicBuddyDbContext>();
        return await db.TrackMetadata.CountAsync();
    }
}

public class RefreshStatusDto
{
    public bool IsRefreshing { get; set; }
    public int TotalFiles { get; set; }
    public int ScannedFiles { get; set; }
    public DateTime? LastRefreshTime { get; set; }
    public string? Error { get; set; }
}
