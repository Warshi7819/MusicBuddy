using Microsoft.Extensions.Caching.Memory;

namespace MusicBuddyAPI.Services;

public class AlbumCatalogService
{
    private const string CacheKey = "albums:all";

    private readonly FileCacheService _fileCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AlbumCatalogService> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private Task<AlbumCatalogPayload>? _warmTask;

    public bool IsRefreshing { get; private set; }
    public int TotalArtists { get; private set; }
    public int ScannedArtists { get; private set; }
    public DateTime? LastRefreshTime { get; private set; }
    public string? CurrentError { get; private set; }

    public AlbumCatalogService(
        FileCacheService fileCache,
        IMemoryCache memoryCache,
        ILogger<AlbumCatalogService> logger)
    {
        _fileCache = fileCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<AlbumCatalogPayload> GetPayloadAsync()
    {
        if (_memoryCache.TryGetValue(CacheKey, out AlbumCatalogPayload? cached) && cached is not null)
            return cached;

        var warmTask = _warmTask;
        if (warmTask is not null) return await warmTask;

        return await WarmAsync();
    }

    public async Task<AlbumCatalogPayload> WarmAsync()
    {
        await _scanLock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(CacheKey, out AlbumCatalogPayload? cached) && cached is not null)
                return cached;

            if (_warmTask is not null) return await _warmTask;

            _warmTask = ScanAsync();
            try
            {
                return await _warmTask;
            }
            finally
            {
                _warmTask = null;
            }
        }
        finally
        {
            _scanLock.Release();
        }
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;

        await _scanLock.WaitAsync();
        try
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            CurrentError = null;
            TotalArtists = 0;
            ScannedArtists = 0;
        }
        finally
        {
            _scanLock.Release();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _memoryCache.Remove(CacheKey);
                await _scanLock.WaitAsync();
                try
                {
                    _memoryCache.Remove(CacheKey);
                    await ScanAsync();
                }
                finally
                {
                    _scanLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Album catalog refresh failed");
                CurrentError = ex.Message;
            }
            finally
            {
                IsRefreshing = false;
            }
        });
    }

    public Task<AlbumCatalogStatusDto> GetStatusAsync()
    {
        return Task.FromResult(new AlbumCatalogStatusDto
        {
            IsRefreshing = IsRefreshing,
            TotalArtists = TotalArtists,
            ScannedArtists = ScannedArtists,
            LastRefreshTime = LastRefreshTime,
            Error = CurrentError
        });
    }

    private async Task<AlbumCatalogPayload> ScanAsync()
    {
        return await Task.Run(() =>
        {
            _logger.LogInformation("Album catalog scan started");

            var root = _fileCache.GetRootPath("mp3");
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                _logger.LogWarning("Album catalog: MP3 root missing or empty ({Root}), returning empty catalog", root);
                var empty = new AlbumCatalogPayload();
                _memoryCache.Set(CacheKey, empty);
                return empty;
            }

            var urlPrefix = _fileCache.GetUrlPrefix("mp3");
            var artists = new List<ArtistDto>();

            var artistDirs = Directory.EnumerateDirectories(root)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToList();
            TotalArtists = artistDirs.Count;
            ScannedArtists = 0;

            foreach (var artistDir in artistDirs)
            {
                var artistName = Path.GetFileName(artistDir);
                var albums = new List<AlbumDto>();

                foreach (var albumDir in Directory.EnumerateDirectories(artistDir)
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
                {
                    var mp3s = Directory.EnumerateFiles(albumDir, "*.mp3")
                        .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (mp3s.Count == 0) continue;

                    var albumName = Path.GetFileName(albumDir);
                    albums.Add(new AlbumDto
                    {
                        Name = albumName,
                        Path = artistName + "/" + albumName,
                        TrackCount = mp3s.Count,
                        FirstTrackPath = urlPrefix + "/" + artistName + "/" + albumName + "/" + Path.GetFileName(mp3s[0])
                    });
                }

                if (albums.Count == 0) continue;

                artists.Add(new ArtistDto
                {
                    Name = artistName,
                    Path = artistName,
                    AlbumCount = albums.Count,
                    Albums = albums.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()
                });

                ScannedArtists++;
            }

            var looseFiles = Directory.EnumerateFiles(root, "*.mp3")
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (looseFiles.Count > 0)
            {
                artists.Add(new ArtistDto
                {
                    Name = "Uncatalogued",
                    Path = "",
                    IsUncatalogued = true,
                    AlbumCount = 1,
                    Albums = new List<AlbumDto>
                    {
                        new AlbumDto
                        {
                            Name = "Uncatalogued",
                            Path = "",
                            TrackCount = looseFiles.Count,
                            FirstTrackPath = urlPrefix + "/" + Path.GetFileName(looseFiles[0])
                        }
                    }
                });
            }

            artists = artists.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var payload = new AlbumCatalogPayload { Artists = artists };
            _memoryCache.Set(CacheKey, payload);

            LastRefreshTime = DateTime.UtcNow;
            _logger.LogInformation("Album catalog scan complete: {Count} artists, {AlbumCount} albums",
                artists.Count, artists.Sum(a => a.AlbumCount));
            return payload;
        });
    }
}

public class AlbumCatalogStatusDto
{
    public bool IsRefreshing { get; set; }
    public int TotalArtists { get; set; }
    public int ScannedArtists { get; set; }
    public DateTime? LastRefreshTime { get; set; }
    public string? Error { get; set; }
}

public class AlbumCatalogPayload
{
    public List<ArtistDto> Artists { get; set; } = new();
}

public class ArtistDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsUncatalogued { get; set; }
    public int AlbumCount { get; set; }
    public List<AlbumDto> Albums { get; set; } = new();
}

public class AlbumDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public string FirstTrackPath { get; set; } = string.Empty;
}