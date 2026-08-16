using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages;

public class ScanModel : PageModel
{
    private static readonly string[] ItunesCountries = ["us", "dk", "gb"];

    private readonly IHttpClientFactory _httpClientFactory;

    public ScanModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetLookupAsync(string code)
    {
        code = (code ?? string.Empty).Trim();

        BarcodeLookupResultDto result;
        if (!Regex.IsMatch(code, @"^\d{8,13}$"))
        {
            result = new BarcodeLookupResultDto();
        }
        else
        {
            result = await LookupCdAsync(code);
        }

        if (result.Found)
        {
            result.Match = await MatchCollectionAsync(result.Artist, result.Title);
        }

        return new JsonResult(result);
    }

    private async Task<BarcodeLookupResultDto> LookupCdAsync(string code)
    {
        var keys = await LoadApiKeysAsync();
        var discogsToken = keys.GetValueOrDefault(SettingKeys.DiscogsToken);
        var providers = !string.IsNullOrEmpty(discogsToken)
            ? new[] { "itunes", "discogs" }
            : new[] { "itunes" };

        foreach (var provider in providers)
        {
            var hit = provider == "discogs"
                ? await SearchDiscogsAsync(code, discogsToken ?? string.Empty)
                : await SearchItunesAsync(code);
            if (hit is not null)
            {
                return hit;
            }
        }

        return new BarcodeLookupResultDto();
    }

    private async Task<BarcodeLookupResultDto?> SearchItunesAsync(string code)
    {
        var variants = new List<string> { code };
        if (code.Length == 13 && code.StartsWith('0'))
        {
            variants.Add(code[1..]);
        }
        else if (code.Length == 12)
        {
            variants.Add("0" + code);
        }

        foreach (var variant in variants)
        {
            foreach (var country in ItunesCountries)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(10);
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        $"https://itunes.apple.com/lookup?upc={Uri.EscapeDataString(variant)}&country={country}");
                    request.Headers.UserAgent.ParseAdd("MusicBuddy/0.1");
                    using var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (!json.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    var item = results[0];
                    var title = GetString(item, "collectionName") ?? GetString(item, "trackName") ?? string.Empty;
                    var artwork = GetString(item, "artworkUrl100") ?? string.Empty;
                    if (artwork.Contains("100x100bb"))
                    {
                        artwork = artwork.Replace("100x100bb", "300x300bb");
                    }

                    return new BarcodeLookupResultDto
                    {
                        Found = true,
                        Title = title,
                        Artist = GetString(item, "artistName") ?? string.Empty,
                        Year = ExtractYear(GetString(item, "releaseDate")) ?? string.Empty,
                        ArtworkUrl = artwork,
                        Source = "iTunes"
                    };
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private async Task<BarcodeLookupResultDto?> SearchDiscogsAsync(string code, string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.discogs.com/database/search?barcode={Uri.EscapeDataString(code)}&type=release");
            request.Headers.Add("Authorization", $"Discogs token={token}");
            request.Headers.UserAgent.ParseAdd("MusicBuddy/0.1");
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!json.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                return null;
            }

            var item = results[0];
            var title = GetString(item, "title") ?? string.Empty;
            var result = new BarcodeLookupResultDto
            {
                Found = true,
                Title = title,
                Year = item.TryGetProperty("year", out var year) && year.ValueKind == JsonValueKind.Number
                    ? year.GetInt32().ToString()
                    : string.Empty,
                ArtworkUrl = GetString(item, "cover_image") ?? string.Empty,
                Source = "Discogs"
            };

            if (title.Contains(" - "))
            {
                var parts = title.Split(" - ", 2);
                result.Artist = parts[0].Trim();
                result.Title = parts[1].Trim();
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private async Task<CollectionMatchResult?> MatchCollectionAsync(string lookupArtist, string lookupTitle)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
            var catalog = await client.GetFromJsonAsync<JsonElement>("/api/albums");
            if (!catalog.TryGetProperty("artists", out var artists) || artists.GetArrayLength() == 0)
            {
                return null;
            }

            var normLookupArtist = Normalize(lookupArtist);
            var normLookupTitle = Normalize(lookupTitle);

            foreach (var artist in artists.EnumerateArray())
            {
                var artistName = GetString(artist, "name") ?? string.Empty;
                var artistPath = GetString(artist, "path") ?? string.Empty;
                var normArtist = Normalize(artistName);

                if (!FuzzyMatch(normLookupArtist, normArtist))
                {
                    continue;
                }

                var artistLink = $"/Albums?artist={Uri.EscapeDataString(artistPath)}";

                if (!artist.TryGetProperty("albums", out var albums))
                {
                    return new CollectionMatchResult
                    {
                        Found = false,
                        ArtistPath = artistPath,
                        ArtistLink = artistLink
                    };
                }

                foreach (var album in albums.EnumerateArray())
                {
                    var albumName = GetString(album, "name") ?? string.Empty;
                    var albumPath = GetString(album, "path") ?? string.Empty;
                    var normAlbum = Normalize(albumName);

                    if (FuzzyMatch(normLookupTitle, normAlbum))
                    {
                        return new CollectionMatchResult
                        {
                            Found = true,
                            ExactAlbum = true,
                            ArtistPath = artistPath,
                            AlbumPath = albumPath,
                            AlbumLink = $"/Albums?artist={Uri.EscapeDataString(artistPath)}&album={Uri.EscapeDataString(albumPath)}",
                            ArtistLink = artistLink
                        };
                    }
                }

                return new CollectionMatchResult
                {
                    Found = false,
                    ArtistPath = artistPath,
                    ArtistLink = artistLink
                };
            }

            return new CollectionMatchResult
            {
                Found = false
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool FuzzyMatch(string normalizedLookup, string normalizedCatalog)
    {
        if (string.IsNullOrEmpty(normalizedLookup) || string.IsNullOrEmpty(normalizedCatalog))
            return false;

        if (normalizedLookup == normalizedCatalog)
            return true;

        var shorter = normalizedLookup.Length <= normalizedCatalog.Length ? normalizedLookup : normalizedCatalog;
        var longer = normalizedLookup.Length <= normalizedCatalog.Length ? normalizedCatalog : normalizedLookup;
        if (shorter.Length >= 3 && longer.Contains(shorter))
            return true;

        var lookupWords = normalizedLookup.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3).ToArray();
        var catalogWords = normalizedCatalog.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3).ToArray();

        if (lookupWords.Length > 0 && catalogWords.Length > 0)
        {
            var matchCount = lookupWords.Count(lw => catalogWords.Any(cw => cw.Contains(lw) || lw.Contains(cw)));
            var minWordCount = Math.Min(lookupWords.Length, catalogWords.Length);
            if (matchCount >= minWordCount && minWordCount > 0)
                return true;
        }

        return false;
    }

    private static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var t = input.ToLowerInvariant().Trim();
        t = Regex.Replace(t, @"\b(the|a|an)\b", " ");
        t = Regex.Replace(t, @"[^a-z0-9\s]", " ");
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }

    private async Task<Dictionary<string, string>> LoadApiKeysAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
            return await client.GetFromJsonAsync<Dictionary<string, string>>("/api/settings") ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ExtractYear(string? date)
        => string.IsNullOrEmpty(date)
            ? null
            : Regex.Match(date, @"(19|20)\d{2}").Value;
}
