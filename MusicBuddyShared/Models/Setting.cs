using System.ComponentModel.DataAnnotations;

namespace MusicBuddyShared.Models;

public class Setting
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
