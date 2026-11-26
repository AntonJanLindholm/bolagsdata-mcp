namespace BolagsdataMcp.Web;

public sealed record FetchedPage(Uri Url, string Html);

/// <summary>Reads a company's start page and its usual contact pages.</summary>
public sealed class WebsiteFetcher(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "web";

    /// <summary>Paths where Swedish company sites usually show their phone number.</summary>
    public static readonly string[] ContactPaths =
        ["/", "/kontakt", "/kontakta-oss", "/contact", "/contact-us", "/om-oss", "/hitta-hit", "/hitta-till-oss"];

    /// <summary>
    /// Turns user input such as "example.se", "www.example.se" or "https://example.se/foo" into a site root.
    /// Returns null for anything that is not a plain http(s) host name.
    /// </summary>
    public static Uri? ToSiteRoot(string input)
    {
        var text = input.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.HostNameType != UriHostNameType.Dns
            || !uri.Host.Contains('.')
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return new UriBuilder(Uri.UriSchemeHttps, uri.IdnHost).Uri;
    }

    /// <summary>Fetches the contact pages of a site. Pages that fail or are not HTML are skipped.</summary>
    public async Task<IReadOnlyList<FetchedPage>> FetchContactPagesAsync(Uri siteRoot, CancellationToken cancellationToken)
    {
        var pages = await FetchPathsAsync(siteRoot, cancellationToken);
        if (pages.Count == 0 && !siteRoot.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            pages = await FetchPathsAsync(new UriBuilder(siteRoot) { Host = "www." + siteRoot.Host }.Uri, cancellationToken);
        }
        return pages;
    }

    private async Task<List<FetchedPage>> FetchPathsAsync(Uri siteRoot, CancellationToken cancellationToken)
    {
        var pages = new List<FetchedPage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ContactPaths)
        {
            var page = await TryFetchAsync(new Uri(siteRoot, path), cancellationToken);
            // Several paths often redirect to the same page. Count each final URL once.
            if (page is not null && seen.Add(page.Url.GetLeftPart(UriPartial.Path).TrimEnd('/')))
            {
                pages.Add(page);
            }
        }
        return pages;
    }

    private async Task<FetchedPage?> TryFetchAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(url, cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || (mediaType is not null && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return new FetchedPage(response.RequestMessage?.RequestUri ?? url, html);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null; // Timeout for this page only.
        }
    }
}
