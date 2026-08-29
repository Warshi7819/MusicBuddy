namespace MusicBuddyAPI.Services;

public class TagLibThrottle
{
    public SemaphoreSlim Art { get; } = new(4, 4);
    public SemaphoreSlim Detail { get; } = new(2, 2);
}