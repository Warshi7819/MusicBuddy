using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages.Settings;

public class ProfileModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ProfileModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ErrorMessage { get; set; } = string.Empty;
    public string? CurrentAlias { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var user = await client.GetFromJsonAsync<LoginResponse>("/api/auth/me");
        CurrentAlias = user?.Alias;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAliasAsync(string? alias)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");

        var response = await client.PutAsJsonAsync("/api/auth/me", new UpdateUserDto { Alias = alias });
        if (response.IsSuccessStatusCode)
        {
            var identity = User.Identity as ClaimsIdentity;
            var existingAlias = User.FindFirst("Alias");
            if (existingAlias is not null)
            {
                identity?.RemoveClaim(existingAlias);
            }

            if (!string.IsNullOrEmpty(alias))
            {
                identity?.AddClaim(new Claim("Alias", alias));
            }
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity!));

            TempData["Success"] = "Alias updated";
        }
        else
        {
            TempData["Error"] = await ApiErrorHelper.ReadApiError(response, "Could not update alias");
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrEmpty(currentPassword))
        {
            ErrorMessage = "Current password is required";
            return Page();
        }
        if (string.IsNullOrEmpty(newPassword))
        {
            ErrorMessage = "New password is required";
            return Page();
        }
        if (newPassword != confirmPassword)
        {
            ErrorMessage = "New password and confirmation do not match";
            return Page();
        }

        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });

        if (response.IsSuccessStatusCode)
        {
            TempData["Success"] = "Password changed";
            return RedirectToPage();
        }

        ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not change password");
        return Page();
    }
}
