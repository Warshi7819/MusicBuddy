namespace MusicBuddyWeb;

public static class ApiErrorHelper
{
    public static async Task<string> ReadApiError(HttpResponseMessage response, string fallback)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            if (json is not null && json.TryGetValue("message", out var message))
            {
                return message;
            }
        }
        catch
        {
        }
        return fallback;
    }
}
