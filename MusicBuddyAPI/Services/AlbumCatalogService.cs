using Microsoft.Extensions.Caching.Memory;

namespace MusicBuddyAPI.Services;

public class AlbumCatalogService
{
    private const string CacheKey = "albums:all";

    private readonly FileCacheService _fileCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AlbumCatalogService> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private Task<object>? _warmTask;

    public AlbumCatalogService(
        FileCacheService fileCache,
        IMemoryCache memoryCache,
        ILogger<AlbumCatalogService> logger)
    {
        _fileCache = fileCache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<object> GetPayloadAsync()
    {
        if (_memoryCache.TryGetValue(CacheKey, out object? cached) && cached is not null)
            return cached;

        var warmTask = _warmTask;
        if (warmTask is not null) return await warmTask;

        return await WarmAsync();
    }

    public async Task<object> WarmAsync()
    {
        await _scanLock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(CacheKey, out object? cached) && cached is not null)
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

    private async Task<object> ScanAsync()
    {
        return await Task.Run(() =>
        {
            _logger.LogInformation("Album catalog scan started");

            var root = _fileCache.GetRootPath("mp3");
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                _logger.LogWarning("Album catalog: MP3 root missing or empty ({Root}), returning empty catalog", root);
                var empty = new { artists = new List<ArtistDto>() };
                _memoryCache.Set(CacheKey, empty);
                return empty;
            }

            var urlPrefix = _fileCache.GetUrlPrefix("mp3");
            var artists = new List<ArtistDto>();

            foreach (var artistDir in Directory.EnumerateDirectories(root)
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
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

            var payload = new { artists };
            _memoryCache.Set(CacheKey, payload);

            _logger.LogInformation("Album catalog scan complete: {Count} artists, {AlbumCount} albums",
                artists.Count, artists.Sum(a => a.AlbumCount));
            return payload;
        });
    }
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