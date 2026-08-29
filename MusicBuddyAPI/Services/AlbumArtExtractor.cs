using Microsoft.Extensions.Caching.Memory;
using System.IO.Enumeration;

namespace MusicBuddyAPI.Services;

public class AlbumArtExtractor
{
    private const string EmptySentinel = "empty";

    public static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public const string PlaceholderEtag = "\"ph\"";

    private readonly FileCacheService _fileCache;
    private readonly IMemoryCache _memoryCache;
    private readonly TagLibThrottle _throttle;
    private readonly ILogger<AlbumArtExtractor> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(12);

    public AlbumArtExtractor(
        FileCacheService fileCache,
        IMemoryCache memoryCache,
        TagLibThrottle throttle,
        ILogger<AlbumArtExtractor> logger)
    {
        _fileCache = fileCache;
        _memoryCache = memoryCache;
        _throttle = throttle;
        _logger = logger;
    }

    private sealed class ArtEntry
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = "image/jpeg";
        public string Etag { get; set; } = "";
        public bool Found => Bytes.Length > 0;
    }

    public async Task<(byte[] Bytes, string MimeType, string Etag)?> GetAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var cacheKey = $"albumart:{path}";
        if (_memoryCache.TryGetValue(cacheKey, out ArtEntry? cached) && cached is not null)
            return cached.Found ? (cached.Bytes!, cached.MimeType, cached.Etag) : null;

        var fullPath = _fileCache.ResolveFilePath(path);
        if (fullPath is null)
        {
            CacheNothing(cacheKey);
            return null;
        }

        await _throttle.Art.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            using var tagFile = TagLib.File.Create(fullPath);
            if (tagFile.Tag.Pictures.Length == 0)
            {
                var folderBytes = FindFolderArt(fullPath);
                if (folderBytes is null)
                {
                    CacheNothing(cacheKey);
                    return null;
                }

                _memoryCache.Set(cacheKey, new ArtEntry { Bytes = folderBytes, MimeType = "image/jpeg", Etag = ComputeEtag(folderBytes) }, _cacheDuration);
                _logger.LogDebug("Album art (folder fallback) for {Path} ({Size} bytes)", path, folderBytes.Length);
                return (folderBytes, "image/jpeg", ComputeEtag(folderBytes));
            }

            var picture = tagFile.Tag.Pictures[0];
            var bytes = picture.Data.Data;
            var mimeType = picture.MimeType ?? "image/jpeg";

            _memoryCache.Set(cacheKey, new ArtEntry { Bytes = bytes, MimeType = mimeType, Etag = ComputeEtag(bytes) }, _cacheDuration);
            _logger.LogDebug("Album art extracted for {Path} ({Size} bytes)", path, bytes.Length);
            return (bytes, mimeType, ComputeEtag(bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            CacheNothing(cacheKey);
            return null;
        }
        finally
        {
            _throttle.Art.Release();
        }
    }

    private void CacheNothing(string cacheKey)
    {
        _memoryCache.Set(cacheKey, new ArtEntry { Bytes = Array.Empty<byte>() }, _cacheDuration);
    }

    private static string ComputeEtag(byte[] bytes)
    {
        return "\"" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes)) + "\"";
    }

    private static byte[]? FindFolderArt(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return null;
        }

        string[] patterns =
        {
            "AlbumArt_*_Large.jpg",
            "Cover.jpg",
            "Folder.jpg"
        };

        var files = Directory.EnumerateFiles(dir).ToList();

        bool HasMatch(string file, string pattern)
        {
            return FileSystemName.MatchesSimpleExpression(
                pattern,
                Path.GetFileName(file),
                ignoreCase: true
            );
        }

        string? firstMatch =
            patterns
                .SelectMany(pattern =>
                    files.Where(f => HasMatch(f, pattern)))
                .FirstOrDefault();

        return firstMatch is null ? null : System.IO.File.ReadAllBytes(firstMatch);
    }
}