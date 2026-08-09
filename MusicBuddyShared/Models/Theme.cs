namespace MusicBuddyShared.Models;

public class Theme
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public string BodyBg { get; set; } = "#ffffff";
    public string BodyColor { get; set; } = "#212529";
    public string CardBg { get; set; } = "#ffffff";
    public string CardBorderColor { get; set; } = "rgba(0,0,0,0.125)";
    public string PrimaryColor { get; set; } = "#0d6efd";
    public string NavbarBg { get; set; } = "#0d6efd";
    public string NavbarTextColor { get; set; } = "#ffffff";
    public string FooterBg { get; set; } = "#f8f9fa";
    public string MutedColor { get; set; } = "#6c757d";
}
