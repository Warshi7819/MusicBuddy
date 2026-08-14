using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class AlbumsModel : PageModel
{
    public string? SelectedArtist { get; set; }
    public string? SelectedAlbum { get; set; }

    public void OnGet(string? artist, string? album)
    {
        SelectedArtist = artist;
        SelectedAlbum = album;
    }
}
