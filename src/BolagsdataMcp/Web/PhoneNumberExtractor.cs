using System.Net;
using System.Text.RegularExpressions;

namespace BolagsdataMcp.Web;

public enum PhoneSource
{
    /// <summary>The number is written in text a visitor can read.</summary>
    VisibleText,

    /// <summary>The number is the target of a clickable tel: link.</summary>
    TelLink,
}

public sealed record PhoneMatch(string E164, string AsWritten, PhoneSource Source);

/// <summary>
/// Finds Swedish phone numbers that a visitor can actually see on a web page.
/// </summary>
/// <remarks>
/// Numbers that only exist in scripts, styles, comments or structured metadata (for example schema.org JSON-LD)
/// are ignored on purpose: such metadata is sometimes copied from another site and names the wrong company.
/// </remarks>
public static partial class PhoneNumberExtractor
{
    public static IReadOnlyList<PhoneMatch> Extract(string html)
    {
        var matches = new List<PhoneMatch>();

        foreach (Match match in PhoneLike().Matches(VisibleText(html)))
        {
            if (ToE164(match.Value) is { } e164)
            {
                matches.Add(new PhoneMatch(e164, CollapseWhitespace(match.Value), PhoneSource.VisibleText));
            }
        }

        foreach (Match match in TelLink().Matches(StripHidden(html)))
        {
            var raw = Uri.UnescapeDataString(match.Groups["number"].Value).Trim();
            if (ToE164(raw) is { } e164)
            {
                matches.Add(new PhoneMatch(e164, raw, PhoneSource.TelLink));
            }
        }

        return matches;
    }

    public static string? GetTitle(string html)
    {
        var match = Title().Match(html);
        return match.Success ? CollapseWhitespace(WebUtility.HtmlDecode(match.Groups["title"].Value)) : null;
    }

    /// <summary>The text a visitor can read: markup, scripts, styles and comments removed.</summary>
    public static string VisibleText(string html)
    {
        var body = HeadSection().Replace(StripHidden(html), " ");
        return WebUtility.HtmlDecode(Tags().Replace(body, " "));
    }

    /// <summary>
    /// Converts a Swedish number to E.164, e.g. "08-123 45 67" to "+4681234567".
    /// Returns null for anything that is not a plausible Swedish phone number.
    /// </summary>
    public static string? ToE164(string raw)
    {
        var text = raw.Replace("(0)", string.Empty, StringComparison.Ordinal).Trim();
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());

        string national;
        if (text.StartsWith('+'))
        {
            if (!digits.StartsWith("46", StringComparison.Ordinal))
            {
                return null; // Only Swedish numbers are supported.
            }
            national = digits[2..];
        }
        else if (digits.StartsWith("0046", StringComparison.Ordinal))
        {
            national = digits[4..];
        }
        else if (digits.StartsWith('0'))
        {
            national = digits[1..];
        }
        else
        {
            return null;
        }

        if (national.StartsWith('0'))
        {
            national = national[1..]; // Written as "+46 08 ...".
        }

        // Swedish national significant numbers have 7 to 9 digits (area code without the leading 0, then the subscriber number).
        if (national.Length is < 7 or > 9 || national.All(c => c == '0'))
        {
            return null;
        }

        return "+46" + national;
    }

    private static string StripHidden(string html) => Comments().Replace(HiddenBlocks().Replace(html, " "), " ");

    private static string CollapseWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"<(script|style|template|noscript)\b[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenBlocks();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex Comments();

    [GeneratedRegex(@"<head\b[\s\S]*?</head\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadSection();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    // Same pattern as the prospecting scripts this server grew out of: +46 numbers, or numbers starting with 0.
    [GeneratedRegex(@"(\+46[\s()0-9-]{7,16}\d|\b0\d{1,3}[\s-]{0,3}\d{2,3}\s?\d{2,3}\s?\d{2,3}\b)")]
    private static partial Regex PhoneLike();

    [GeneratedRegex(@"href\s*=\s*[""']tel:(?<number>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex TelLink();

    [GeneratedRegex(@"<title[^>]*>(?<title>[\s\S]*?)</title\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex Title();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
