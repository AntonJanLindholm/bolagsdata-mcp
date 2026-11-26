using System.ComponentModel;
using BolagsdataMcp.Web;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace BolagsdataMcp.Tools;

[McpServerToolType]
public sealed class WebsiteTools(WebsiteFetcher fetcher)
{
    [McpServerTool(Name = "find_phone_numbers", Title = "Find phone numbers on a company website", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Reads the start page and the usual contact pages (kontakt, kontakta-oss, om-oss and so on) of a company website " +
        "and returns the Swedish phone numbers a visitor can see there, with the pages they were found on. " +
        "Numbers hidden in scripts or metadata are ignored. " +
        "Check that pageTitle matches the company before trusting the result: a guessed domain can belong to someone else.")]
    public async Task<WebsitePhoneResult> FindPhoneNumbers(
        [Description("The company's website, e.g. 'example.se' or 'https://www.example.se'.")] string website,
        CancellationToken cancellationToken)
    {
        var siteRoot = WebsiteFetcher.ToSiteRoot(website)
            ?? throw new McpException("Give a public website address such as 'example.se'. IP addresses and other schemes are not allowed.");

        var pages = await fetcher.FetchContactPagesAsync(siteRoot, cancellationToken);

        var numbers = pages
            .SelectMany(page => PhoneNumberExtractor.Extract(page.Html).Select(match => (page, match)))
            .GroupBy(x => x.match.E164)
            .Select(group => new FoundPhoneNumber(
                Number: group.Key,
                AsWritten: group.Select(x => x.match.AsWritten).Distinct().ToList(),
                FoundOn: group.Select(x => x.page.Url.ToString()).Distinct().ToList(),
                InVisibleText: group.Any(x => x.match.Source == PhoneSource.VisibleText),
                AsTelLink: group.Any(x => x.match.Source == PhoneSource.TelLink)))
            .OrderByDescending(n => n.FoundOn.Count)
            .ThenBy(n => n.Number, StringComparer.Ordinal)
            .ToList();

        var notes = new List<string>();
        if (pages.Count == 0)
        {
            notes.Add("No page could be read. The site may be down, block automated visits or need JavaScript. Check it by hand.");
        }
        else if (numbers.Count == 0)
        {
            notes.Add("No phone number is visible on these pages. The site may load its contact details with JavaScript. Check it by hand.");
        }

        return new WebsitePhoneResult(
            Website: siteRoot.Host,
            PageTitle: pages.Select(p => PhoneNumberExtractor.GetTitle(p.Html)).FirstOrDefault(t => !string.IsNullOrEmpty(t)),
            PagesRead: pages.Select(p => p.Url.ToString()).ToList(),
            Numbers: numbers,
            Notes: notes);
    }
}

public sealed record WebsitePhoneResult(
    string Website,
    string? PageTitle,
    IReadOnlyList<string> PagesRead,
    IReadOnlyList<FoundPhoneNumber> Numbers,
    IReadOnlyList<string> Notes);

public sealed record FoundPhoneNumber(
    string Number,
    IReadOnlyList<string> AsWritten,
    IReadOnlyList<string> FoundOn,
    bool InVisibleText,
    bool AsTelLink);
