using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages.Settings;

public class VuMetersModel : PageModel
{
    private const string CookieName = "MusicBuddyVUMeter";
    private static readonly string[] AllowedStyles = ["classic", "glow", "blueglow", "greenglow", "flatgold"];

    public string CurrentStyle { get; set; } = "classic";

    public void OnGet()
    {
        CurrentStyle = GetValidatedStyle(Request.Cookies[CookieName]);
    }

    public IActionResult OnPostSelectAsync(string style)
    {
        style = GetValidatedStyle(style);
        Response.Cookies.Append(CookieName, style, new CookieOptions
        {
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });
        return RedirectToPage();
    }

    private static string GetValidatedStyle(string? style)
    {
        return Array.IndexOf(AllowedStyles, style) >= 0 ? style! : "classic";
    }
}