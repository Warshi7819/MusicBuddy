using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MusicBuddyShared.Dtos;

namespace MusicBuddyWeb.Pages.Settings;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public UsersModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<UserDto> Users { get; set; } = [];
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await LoadUsers();
    }

    public async Task<IActionResult> OnPostCreateAsync(string newUsername, string? newAlias, string newPassword, bool newIsAdmin)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserDto
        {
            Username = newUsername,
            Alias = newAlias,
            Password = newPassword,
            IsAdmin = newIsAdmin
        });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not create user");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAdminAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var user = await client.GetFromJsonAsync<UserDto>($"/api/users/{id}");
        if (user is null)
        {
            return NotFound();
        }

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new UpdateUserDto
        {
            IsAdmin = !user.IsAdmin
        });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not update user");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleDisableAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var user = await client.GetFromJsonAsync<UserDto>($"/api/users/{id}");
        if (user is null)
        {
            return NotFound();
        }

        var response = await client.PutAsJsonAsync($"/api/users/{id}", new UpdateUserDto
        {
            IsDisabled = !user.IsDisabled
        });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not update user");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetAliasAsync(int id, string? alias)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.PutAsJsonAsync($"/api/users/{id}", new UpdateUserDto
        {
            Alias = alias
        });

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not update alias");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        var response = await client.DeleteAsync($"/api/users/{id}");

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ApiErrorHelper.ReadApiError(response, "Could not delete user");
        }

        return RedirectToPage();
    }

    public bool IsLastEnabledAdmin(UserDto user)
    {
        if (!user.IsAdmin || user.IsDisabled)
        {
            return false;
        }
        return Users.Count(u => u.IsAdmin && !u.IsDisabled && u.Id != user.Id) == 0;
    }

    private async Task LoadUsers()
    {
        var client = _httpClientFactory.CreateClient("MusicBuddyAPI");
        Users = await client.GetFromJsonAsync<List<UserDto>>("/api/users") ?? [];
    }
}
