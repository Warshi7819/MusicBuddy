using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages.Settings;

public class ThemesModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ThemesModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<ThemeDto> Themes { get; set; } = [];
    public int? ActiveThemeId { get; set; }
    public ThemeDto? EditTheme { get; set; }
    public bool IsEditing => Request.Query.ContainsKey("edit");
    public bool IsCopying => Request.Query.ContainsKey("copy");

    public async Task OnGetAsync()
    {
        ActiveThemeId = GetActiveThemeId();
        await LoadThemes();

        if (IsEditing)
        {
            var sourceId = int.Parse(Request.Query["edit"]!);
            var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
            EditTheme = await client.GetFromJsonAsync<ThemeDto>($"/api/themes/{sourceId}");
        }
        else if (IsCopying)
        {
            var sourceId = int.Parse(Request.Query["copy"]!);
            var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
            EditTheme = await client.GetFromJsonAsync<ThemeDto>($"/api/themes/{sourceId}");
        }
    }

    public async Task<IActionResult> OnPostSelectAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var theme = await client.GetFromJsonAsync<ThemeDto>($"/api/themes/{id}");
        if (theme is null)
        {
            return RedirectToPage();
        }

        SetThemeCookie(theme);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync(ThemeDto dto)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PostAsJsonAsync("/api/themes", dto);
        if (!response.IsSuccessStatusCode)
        {
            return RedirectToPage();
        }

        var created = await response.Content.ReadFromJsonAsync<ThemeDto>();
        if (created is not null)
        {
            SetThemeCookie(created);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, ThemeDto dto)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PutAsJsonAsync($"/api/themes/{id}", dto);
        if (!response.IsSuccessStatusCode)
        {
            return RedirectToPage();
        }

        if (GetActiveThemeId() == id)
        {
            var updated = await client.GetFromJsonAsync<ThemeDto>($"/api/themes/{id}");
            if (updated is not null)
            {
                SetThemeCookie(updated);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        await client.DeleteAsync($"/api/themes/{id}");

        if (GetActiveThemeId() == id)
        {
            Response.Cookies.Delete("MusicBuddyTheme");
        }

        return RedirectToPage();
    }

    private int? GetActiveThemeId()
    {
        var cookie = Request.Cookies["MusicBuddyTheme"];
        if (string.IsNullOrEmpty(cookie))
        {
            return null;
        }

        if (cookie == "light")
        {
            return 1;
        }

        if (cookie == "dark")
        {
            return 2;
        }
        if (cookie.StartsWith('{'))
        {
            try { return JsonSerializer.Deserialize<JsonElement>(cookie).GetProperty("Id").GetInt32(); }
            catch { return null; }
        }
        return null;
    }

    private void SetThemeCookie(ThemeDto theme)
    {
        string cookieValue;
        if (theme.IsBuiltIn)
        {
            cookieValue = theme.Name.ToLowerInvariant();
        }
        else
        {
            cookieValue = JsonSerializer.Serialize(new
            {
                Id = theme.Id,
                theme.BodyBg, theme.BodyColor, theme.CardBg, theme.CardBorderColor,
                theme.PrimaryColor, theme.NavbarBg, theme.NavbarTextColor,
                theme.FooterBg, theme.MutedColor
            });
        }

        Response.Cookies.Append("MusicBuddyTheme", cookieValue, new CookieOptions
        {
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });
    }

    private async Task LoadThemes()
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        Themes = await client.GetFromJsonAsync<List<ThemeDto>>("/api/themes") ?? [];
    }
}
