using System.Collections.Concurrent;
using System.Text;
using MusicBuddyAPI.Controllers;

namespace MusicBuddyAPI.Services;

public class RandomTrackService
{
    private readonly FileCacheService _fileCache;
    private readonly ILogger<RandomTrackService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private List<TrackIndexEntry> _allEntries = new();
    private Dictionary<string, List<TrackIndexEntry>> _genreIndex = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _allGenres = new();
    private bool _built;

    public RandomTrackService(FileCacheService fileCache, ILogger<RandomTrackService> logger)
    {
        _fileCache = fileCache;
        _logger = logger;
    }

    public async Task WarmAsync()
    {
        if (_built) return;

        await _lock.WaitAsync();
        try
        {
            if (_built) return;
            await BuildIndexAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await BuildIndexAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RandomTrackResult?> GetRandomAsync()
    {
        await EnsureBuiltAsync();

        List<TrackIndexEntry> entries;
        await _lock.WaitAsync();
        try { entries = _allEntries; }
        finally { _lock.Release(); }

        if (entries.Count == 0) return null;

        var rng = Random.Shared;
        var entry = entries[rng.Next(entries.Count)];
        return await ResolveTrackResultAsync(entry);
    }

    public async Task<RandomTrackResult?> GetRandomByArtistAsync(string artistName)
    {
        await EnsureBuiltAsync();

        List<TrackIndexEntry> filtered;
        await _lock.WaitAsync();
        try
        {
            filtered = _allEntries
                .Where(e => string.Equals(e.ArtistFolderName, artistName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        finally { _lock.Release(); }

        if (filtered.Count == 0) return null;

        var rng = Random.Shared;
        var entry = filtered[rng.Next(filtered.Count)];
        return await ResolveTrackResultAsync(entry);
    }

    public async Task<RandomTrackResult?> GetRandomByGenreAsync(string genre)
    {
        await EnsureBuiltAsync();

        List<TrackIndexEntry>? genreEntries;
        await _lock.WaitAsync();
        try
        {
            _genreIndex.TryGetValue(genre, out genreEntries);
        }
        finally { _lock.Release(); }

        if (genreEntries == null || genreEntries.Count == 0) return null;

        var rng = Random.Shared;
        var entry = genreEntries[rng.Next(genreEntries.Count)];
        return await ResolveTrackResultAsync(entry);
    }

    public async Task<List<string>> GetGenresAsync()
    {
        await EnsureBuiltAsync();

        await _lock.WaitAsync();
        try { return _allGenres.ToList(); }
        finally { _lock.Release(); }
    }

    private async Task EnsureBuiltAsync()
    {
        if (_built) return;
        await WarmAsync();
    }

    private async Task BuildIndexAsync()
    {
        var root = _fileCache.GetRootPath("mp3");
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            _logger.LogWarning("RandomTrackService: MP3 root missing ({Root}), building empty index", root);
            _allEntries = new();
            _genreIndex = new(StringComparer.OrdinalIgnoreCase);
            _allGenres = new();
            _built = true;
            return;
        }

        var urlPrefix = _fileCache.GetUrlPrefix("mp3");
        var entries = new List<TrackIndexEntry>();
        var genreMap = new Dictionary<string, List<TrackIndexEntry>>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("RandomTrackService: building track index from {Root}", root);

        var allFiles = await Task.Run(() =>
            Directory.EnumerateFiles(root, "*.mp3", SearchOption.AllDirectories).ToList());

        foreach (var filePath in allFiles)
        {
            var relativeFromRoot = Path.GetRelativePath(root, filePath).Replace('\\', '/');
            var parts = relativeFromRoot.Split('/', 2);
            bool isLooseFile = !relativeFromRoot.Contains('/');
            string artistFolder;
            string albumRelative;
            string albumPath;
            if (isLooseFile)
            {
                artistFolder = "Uncatalogued";
                albumRelative = "";
                albumPath = "";
            }
            else
            {
                artistFolder = parts[0];
                albumRelative = parts.Length > 1 ? parts[1].Split('/')[0] : "";
                albumPath = artistFolder + "/" + albumRelative;
            }
            var albumName = isLooseFile ? "Uncatalogued" : Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "";
            var urlPath = urlPrefix + "/" + relativeFromRoot;

            string? genre = null;
            string? trackName = null;
            int durationSeconds = 0;

            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var g = tagFile.Tag.Genres?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(g)) genre = g;
                trackName = tagFile.Tag.Title;
                durationSeconds = (int)tagFile.Properties.Duration.TotalSeconds;
            }
            catch
            {
                // Tag reading failed, continue with defaults
            }

            if (string.IsNullOrWhiteSpace(trackName))
                trackName = Path.GetFileNameWithoutExtension(filePath);

            var entry = new TrackIndexEntry
            {
                FilePath = filePath,
                UrlPath = urlPath,
                ArtistFolderName = artistFolder,
                AlbumPath = albumPath,
                AlbumName = albumName,
                TrackName = trackName,
                Genre = genre,
                DurationSeconds = durationSeconds
            };

            entries.Add(entry);

            if (!string.IsNullOrWhiteSpace(genre))
            {
                if (!genreMap.TryGetValue(genre, out var list))
                {
                    list = new List<TrackIndexEntry>();
                    genreMap[genre] = list;
                }
                list.Add(entry);
            }
        }

        var sortedGenres = genreMap.Keys.OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList();

        _allEntries = entries;
        _genreIndex = genreMap;
        _allGenres = sortedGenres;
        _built = true;

        _logger.LogInformation("RandomTrackService: index built — {Count} tracks, {GenreCount} genres",
            entries.Count, sortedGenres.Count);
    }

    private async Task<RandomTrackResult?> ResolveTrackResultAsync(TrackIndexEntry entry)
    {
        try
        {
            var albumDir = Path.GetDirectoryName(entry.FilePath);
            if (string.IsNullOrEmpty(albumDir))
                return CreateFallbackResult(entry, 0);

            var trackFiles = await Task.Run(() =>
                Directory.EnumerateFiles(albumDir, "*.mp3")
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList());

            var trackIndex = trackFiles.FindIndex(f =>
                string.Equals(f, entry.FilePath, StringComparison.OrdinalIgnoreCase));

            if (trackIndex < 0) trackIndex = 0;

            return new RandomTrackResult
            {
                Track = new TrackDto
                {
                    Name = entry.TrackName,
                    Path = entry.UrlPath,
                    DurationSeconds = entry.DurationSeconds
                },
                ArtistName = entry.ArtistFolderName,
                AlbumPath = entry.AlbumPath,
                Genre = entry.Genre,
                TrackIndex = trackIndex
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving track result for {Path}", entry.FilePath);
            return CreateFallbackResult(entry, 0);
        }
    }

    private static RandomTrackResult CreateFallbackResult(TrackIndexEntry entry, int trackIndex)
    {
        return new RandomTrackResult
        {
            Track = new TrackDto
            {
                Name = entry.TrackName,
                Path = entry.UrlPath,
                DurationSeconds = entry.DurationSeconds
            },
            ArtistName = entry.ArtistFolderName,
            AlbumPath = entry.AlbumPath,
            Genre = entry.Genre,
            TrackIndex = trackIndex
        };
    }

    private class TrackIndexEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public string UrlPath { get; set; } = string.Empty;
        public string ArtistFolderName { get; set; } = string.Empty;
        public string AlbumPath { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string? Genre { get; set; }
        public int DurationSeconds { get; set; }
    }
}

public class RandomTrackResult
{
    public TrackDto Track { get; set; } = new();
    public string ArtistName { get; set; } = string.Empty;
    public string AlbumPath { get; set; } = string.Empty;
    public string? Genre { get; set; }
    public int TrackIndex { get; set; }
}
