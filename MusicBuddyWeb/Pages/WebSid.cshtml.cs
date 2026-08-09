using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MusicBuddyWeb.Pages;

public class WebSidModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public List<FileEntry> Files { get; set; } = new();

    public WebSidModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public void OnGet()
    {
        var dir = Path.Combine(_env.WebRootPath, "Music", "Sid");
        if (Directory.Exists(dir))
        {
            Files = Directory.GetFiles(dir, "*.sid", SearchOption.AllDirectories)
                .Select(f => new FileEntry
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    Path = "/" + Path.GetRelativePath(_env.WebRootPath, f).Replace('\\', '/'),
                    Size = new FileInfo(f).Length
                })
                .OrderBy(f => f.Name)
                .ToList();
        }
    }

    public class FileEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long Size { get; set; }
    }
}
