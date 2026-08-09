namespace MusicBuddyWeb;

public class CookieForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var authCookie = httpContext?.Request.Cookies["MusicBuddyAuth"];

        if (!string.IsNullOrEmpty(authCookie))
        {
            request.Headers.Add("Cookie", $"MusicBuddyAuth={authCookie}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
