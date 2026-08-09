using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages;

[EnableRateLimiting("LoginPolicy")]
public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string ErrorMessage { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string username, string password)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Invalid username or password";
            return Page();
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            ErrorMessage = "This account has been disabled";
            return Page();
        }
        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Login failed. Please try again.";
            return Page();
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null)
        {
            ErrorMessage = "Login failed. Please try again.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Id.ToString()),
            new(ClaimTypes.Name, result.Username)
        };
        if (!string.IsNullOrEmpty(result.Alias))
        {
            claims.Add(new Claim("Alias", result.Alias));
        }
        if (result.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
