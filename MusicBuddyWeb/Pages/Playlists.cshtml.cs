using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class PlaylistsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PlaylistsModel> _logger;

    public PlaylistsModel(IHttpClientFactory http, ILogger<PlaylistsModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string FileType { get; set; } = "mp3";

    public void OnGet()
    {
        FileType = Request.Query["type"].FirstOrDefault() ?? "mp3";
    }

    public async Task<IActionResult> OnGetPlaylistListAsync(string type = "mp3")
    {
        var playlists = await GetPlaylistsAsync(type);
        return Partial("Playlists/_PlaylistSelect", new PlaylistSelectModel
        {
            FileType = type,
            Playlists = playlists
        });
    }

    public async Task<IActionResult> OnGetTrackListAsync(int playlistId)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var detail = await client.GetFromJsonAsync<PlaylistDetail>($"/api/playlists/{playlistId}");
            return Partial("Playlists/_PlaylistTrackList", new PlaylistTrackListModel
            {
                PlaylistId = playlistId,
                Tracks = detail?.Tracks ?? new()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading playlist tracks");
            return Content("<div class='text-danger py-3'>Error loading tracks.</div>");
        }
    }

    public async Task<IActionResult> OnGetLibraryAsync(string type = "mp3", string path = "")
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var listing = await client.GetFromJsonAsync<DirectoryListing>(
                $"/api/filebrowser/browse?type={Uri.EscapeDataString(type ?? "mp3")}&path={Uri.EscapeDataString(path ?? "")}");
            return Partial("Playlists/_LibraryBrowser", new LibraryBrowserModel
            {
                FileType = type ?? "mp3",
                CurrentPath = path ?? "",
                Listing = listing ?? new DirectoryListing()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing library");
            return Content("<div class='text-danger py-3'>Error loading directory.</div>");
        }
    }

    public async Task<IActionResult> OnGetSearchAsync(string? song = null, string? artist = null, string? genre = null, string type = "mp3", int skip = 0, int take = 50, string? sortBy = null, bool sortAsc = true)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(song)) qs.Add($"song={Uri.EscapeDataString(song)}");
            if (!string.IsNullOrWhiteSpace(artist)) qs.Add($"artist={Uri.EscapeDataString(artist)}");
            if (!string.IsNullOrWhiteSpace(genre)) qs.Add($"genre={Uri.EscapeDataString(genre)}");
            qs.Add($"skip={skip}");
            qs.Add($"take={take}");
            if (!string.IsNullOrWhiteSpace(sortBy)) qs.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
            qs.Add($"sortAsc={sortAsc.ToString().ToLowerInvariant()}");
            var url = "/api/metadata/search?" + string.Join("&", qs);

            var response = await client.GetFromJsonAsync<SearchResponse>(url);
            return Partial("Playlists/_LibraryBrowserSearch", new TrackSearchListModel
            {
                FileType = type ?? "mp3",
                Song = song,
                Artist = artist,
                Genre = genre,
                Results = response?.Items ?? new(),
                TotalCount = response?.TotalCount ?? 0,
                Skip = skip,
                Take = take,
                SortBy = sortBy,
                SortAsc = sortAsc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching metadata");
            return Content("<div class='text-danger py-3'>Error searching.</div>");
        }
    }

    public async Task<IActionResult> OnPostCreateAsync([FromBody] CreatePlaylistRequest request)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var response = await client.PostAsJsonAsync("/api/playlists", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ApiErrorHelper.ReadApiError(response, "Failed to create playlist");
                return BadRequest(new { message = error });
            }
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistSummary>();
            return new JsonResult(new { id = playlist?.Id, name = playlist?.Name, fileType = request.FileType });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            return StatusCode(500, new { message = "Error creating playlist" });
        }
    }

    public async Task<IActionResult> OnPostRenameAsync(int id, string name, string type = "mp3")
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var response = await client.PutAsJsonAsync($"/api/playlists/{id}", new { name });
            if (!response.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to rename playlist" });

            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming playlist");
            return StatusCode(500, new { message = "Error renaming playlist" });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string type = "mp3")
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            await client.DeleteAsync($"/api/playlists/{id}");
            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist");
            return StatusCode(500, new { message = "Error deleting playlist" });
        }
    }

    public async Task<IActionResult> OnPostClearAsync(int id)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            await client.DeleteAsync($"/api/playlists/{id}/tracks");
            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing playlist");
            return StatusCode(500, new { message = "Error clearing playlist" });
        }
    }

    public async Task<IActionResult> OnPostAddTracksAsync(int playlistId, [FromBody] List<AddTrackRequest> tracks)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var response = await client.PostAsJsonAsync($"/api/playlists/{playlistId}/tracks", tracks);
            if (!response.IsSuccessStatusCode)
                return BadRequest(new { message = "Failed to add tracks" });

            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tracks");
            return StatusCode(500, new { message = "Error adding tracks" });
        }
    }

    public async Task<IActionResult> OnPostRemoveTrackAsync(int playlistId, int trackId)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            await client.DeleteAsync($"/api/playlists/{playlistId}/tracks/{trackId}");
            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing track");
            return StatusCode(500, new { message = "Error removing track" });
        }
    }

    public async Task<IActionResult> OnPostReorderAsync(int playlistId, [FromBody] List<ReorderTrackRequest> reorder)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            await client.PutAsJsonAsync($"/api/playlists/{playlistId}/tracks/reorder", reorder);
            return new StatusCodeResult(204);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering tracks");
            return StatusCode(500, new { message = "Error reordering tracks" });
        }
    }

    public async Task<IActionResult> OnPostCollectFilesAsync(string type, string path)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            var files = await client.GetFromJsonAsync<List<FileEntry>>(
                $"/api/filebrowser/collect?type={Uri.EscapeDataString(type ?? "mp3")}&path={Uri.EscapeDataString(path ?? "")}");
            return new JsonResult(files ?? new());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting files");
            return StatusCode(500, new { message = "Error collecting files" });
        }
    }

    private async Task<List<PlaylistSummary>> GetPlaylistsAsync(string type)
    {
        var client = _http.CreateClient("MusicBuddyAPI");
        try
        {
            return await client.GetFromJsonAsync<List<PlaylistSummary>>($"/api/playlists?type={Uri.EscapeDataString(type)}") ?? new();
        }
        catch
        {
            return new();
        }
    }
}

// Request DTOs
public class CreatePlaylistRequest
{
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = "mp3";
}

public class AddTrackRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public int ChannelCount { get; set; } = 2;
}

public class ReorderTrackRequest
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}

// Response DTOs
public class PlaylistSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class PlaylistDetail
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public List<PlaylistTrackDto> Tracks { get; set; } = new();
}

public class PlaylistTrackDto
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? TrackName { get; set; }
    public string? Artist { get; set; }
    public long FileSize { get; set; }
    public int ChannelCount { get; set; } = 2;
    public int SortOrder { get; set; }
}

// View models
public class PlaylistSelectModel
{
    public string FileType { get; set; } = "mp3";
    public List<PlaylistSummary> Playlists { get; set; } = new();
    public int? SelectedId { get; set; }
}

public class PlaylistTrackListModel
{
    public int PlaylistId { get; set; }
    public List<PlaylistTrackDto> Tracks { get; set; } = new();
}

public class LibraryBrowserModel
{
    public string FileType { get; set; } = "mp3";
    public string CurrentPath { get; set; } = "";
    public DirectoryListing Listing { get; set; } = new();
}

public class TrackSearchResult
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string UrlPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? TrackName { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int DurationSeconds { get; set; }
    public int ChannelCount { get; set; } = 2;
    public string ArtistFolder { get; set; } = string.Empty;
}

public class TrackSearchListModel
{
    public string FileType { get; set; } = "mp3";
    public string? Song { get; set; }
    public string? Artist { get; set; }
    public string? Genre { get; set; }
    public List<TrackSearchResult> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortAsc { get; set; } = true;
}

public class SearchResponse
{
    public int TotalCount { get; set; }
    public List<TrackSearchResult> Items { get; set; } = new();
}
