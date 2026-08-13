using Microsoft.Extensions.Caching.Memory;

namespace MusicBuddyAPI.Services;

public class FileEntry
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public int ChannelCount { get; set; } = 2;
}

public class DirectoryListing
{
    public string CurrentPath { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
    public List<FileEntry> Directories { get; set; } = new();
    public List<FileEntry> Files { get; set; } = new();
}

public class FileCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<FileCacheService> _logger;
    private readonly TimeSpan _defaultDuration;
    private readonly string _mp3Root;
    private readonly string _sidRoot;
    private readonly string _mp3UrlPrefix;
    private readonly string _sidUrlPrefix;

    public FileCacheService(
        IMemoryCache cache,
        ILogger<FileCacheService> logger,
        IConfiguration config)
    {
        _cache = cache;
        _logger = logger;

        var durationMinutes = config.GetValue<int>("FileCache:DefaultDurationMinutes", 10);
        _defaultDuration = TimeSpan.FromMinutes(durationMinutes);

        _mp3Root = ResolvePath(config["Music:Mp3Root"] ?? string.Empty);
        _sidRoot = ResolvePath(config["Music:SidRoot"] ?? string.Empty);
        _mp3UrlPrefix = config["Music:Mp3UrlPrefix"] ?? "/Music/Mp3";
        _sidUrlPrefix = config["Music:SidUrlPrefix"] ?? "/Music/Sid";

        _logger.LogInformation("FileCache initialized: MP3 root={Mp3Root}, SID root={SidRoot}, cache duration={Duration}min",
            _mp3Root, _sidRoot, _defaultDuration.TotalMinutes);
    }

    public string GetRootPath(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            "mp3" => _mp3Root,
            "sid" => _sidRoot,
            _ => throw new ArgumentException($"Unknown file type: {fileType}")
        };
    }

    public string GetUrlPrefix(string fileType)
    {
        return fileType.ToLowerInvariant() switch
        {
            "mp3" => _mp3UrlPrefix,
            "sid" => _sidUrlPrefix,
            _ => throw new ArgumentException($"Unknown file type: {fileType}")
        };
    }

    public string CacheKey(string fullPath, string fileType)
    {
        return $"files:{fileType}:{fullPath}";
    }

    public async Task<DirectoryListing> BrowseAsync(string relativePath, string fileType)
    {
        var rootPath = GetRootPath(fileType);
        if (string.IsNullOrEmpty(rootPath))
        {
            throw new InvalidOperationException($"No root path configured for file type '{fileType}'");
        }

        var normalizedRelative = relativePath.TrimStart('/').Replace('\\', '/');
        var fullPath = Path.Combine(rootPath, normalizedRelative.Replace('/', Path.DirectorySeparatorChar));

        _logger.LogDebug("BrowseAsync: type={FileType}, relativePath={RelativePath}, resolved={FullPath}",
            fileType, relativePath, fullPath);

        if (!Directory.Exists(fullPath))
        {
            _logger.LogWarning("Directory does not exist: {FullPath}", fullPath);
            return new DirectoryListing { CurrentPath = relativePath };
        }

        var cacheKey = CacheKey(fullPath, fileType);
        if (_cache.TryGetValue(cacheKey, out DirectoryListing? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for {FullPath}", fullPath);
            return cached;
        }

        _logger.LogDebug("Cache miss for {FullPath}, scanning directory", fullPath);
        var listing = await ScanDirectoryAsync(fullPath, normalizedRelative, fileType);
        _cache.Set(cacheKey, listing, _defaultDuration);

        _logger.LogInformation("Cached directory listing for {Path}: {DirCount} dirs, {FileCount} files (expires in {Duration})",
            relativePath, listing.Directories.Count, listing.Files.Count, _defaultDuration);
        return listing;
    }

    public void RefreshCache(string fileType)
    {
        var rootPath = GetRootPath(fileType);
        if (string.IsNullOrEmpty(rootPath)) return;

        var cacheKey = CacheKey(rootPath, fileType);
        _cache.Remove(cacheKey);
        _logger.LogInformation("Evicted root cache for {FileType}", fileType);
    }

    public void RefreshCache(string fullPath, string fileType)
    {
        var cacheKey = CacheKey(fullPath, fileType);
        _cache.Remove(cacheKey);
        _logger.LogInformation("Evicted cache for {Path}", fullPath);
    }

    public TimeSpan GetCurrentDuration() => _defaultDuration;

    public string? ResolveFilePath(string urlPath)
    {
        if (string.IsNullOrEmpty(urlPath)) return null;

        var normalized = urlPath.Replace('\\', '/').TrimStart('/');

        foreach (var (urlPrefix, rootPath) in new[] { (_mp3UrlPrefix, _mp3Root), (_sidUrlPrefix, _sidRoot) })
        {
            if (string.IsNullOrEmpty(urlPrefix)) continue;
            var prefix = urlPrefix.TrimStart('/').TrimEnd('/');
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = normalized[prefix.Length..].TrimStart('/');
            if (string.IsNullOrEmpty(relative)) continue;

            var fullPath = Path.Combine(rootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath)) return fullPath;
        }

        return null;
    }

    private async Task<DirectoryListing> ScanDirectoryAsync(string fullPath, string relativePath, string fileType)
    {
        var urlPrefix = GetUrlPrefix(fileType);

        return await Task.Run(() =>
        {
            var listing = new DirectoryListing
            {
                CurrentPath = "/" + relativePath,
                ParentPath = string.IsNullOrEmpty(relativePath) || relativePath == "/"
                    ? null
                    : ComputeParentPath(relativePath)
            };

            try
            {
                var dirs = Directory.GetDirectories(fullPath)
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);
                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    listing.Directories.Add(new FileEntry
                    {
                        Name = dirName,
                        Path = string.IsNullOrEmpty(relativePath)
                            ? dirName
                            : relativePath.TrimEnd('/') + "/" + dirName,
                        IsDirectory = true
                    });
                }

                var searchPattern = fileType.ToLowerInvariant() == "mp3" ? "*.mp3" : "*.sid";
                var files = Directory.GetFiles(fullPath, searchPattern)
                    .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var relativeFilePath = Path.GetRelativePath(fullPath, file).Replace('\\', '/');
                    listing.Files.Add(new FileEntry
                    {
                        Name = fileName,
                        Path = urlPrefix + "/" + (string.IsNullOrEmpty(relativePath) ? relativeFilePath : relativePath.TrimEnd('/') + "/" + Path.GetFileName(file)),
                        Size = new FileInfo(file).Length,
                        IsDirectory = false,
                        ChannelCount = fileType == "mp3" ? ReadChannelCount(file) : 2
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory {Path}", fullPath);
            }

            return listing;
        });
    }

    private static string ComputeParentPath(string relativePath)
    {
        var segments = relativePath.Trim('/').Split('/');
        return segments.Length <= 1 ? "" : string.Join('/', segments.Take(segments.Length - 1));
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        if (path == "~" || path.StartsWith("~/"))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        path = Environment.ExpandEnvironmentVariables(path);

        return Path.GetFullPath(path);
    }

    private int ReadChannelCount(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            return tagFile.Properties.AudioChannels;
        }
        catch
        {
            return 2;
        }
    }
}
