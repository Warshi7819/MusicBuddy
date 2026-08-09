using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class Mp3Model : PageModel
{
    public List<FileEntry> Files { get; set; } = new();

    public void OnGet()
    {
    }

    public class FileEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long Size { get; set; }
    }
}
