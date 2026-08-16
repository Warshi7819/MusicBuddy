using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages.Settings;

[Authorize(Roles = "Admin")]
public class ApiKeysModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiKeysModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string DiscogsToken { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Saved { get; set; }

    public async Task OnGetAsync()
    {
        await LoadKeys();
    }

    public async Task<IActionResult> OnPostSaveAsync(string discogsToken)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PutAsJsonAsync(
            $"/api/settings/{SettingKeys.DiscogsToken}",
            new SettingDto { Key = SettingKeys.DiscogsToken, Value = discogsToken?.Trim() ?? string.Empty });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Could not save Discogs token.";
        }
        else
        {
            Saved = true;
        }

        await LoadKeys();
        return Page();
    }

    private async Task LoadKeys()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
            var settings = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/settings") ?? [];
            DiscogsToken = settings.GetValueOrDefault(SettingKeys.DiscogsToken) ?? string.Empty;
        }
        catch
        {
        }
    }
}
