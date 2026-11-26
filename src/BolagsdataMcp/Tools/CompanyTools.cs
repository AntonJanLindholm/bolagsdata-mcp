using System.ComponentModel;
using BolagsdataMcp.Scb;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace BolagsdataMcp.Tools;

[McpServerToolType]
public sealed class CompanyTools(ScbAfrClient scb)
{
    [McpServerTool(Name = "get_company", Title = "Look up a Swedish company", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Looks up a Swedish company (legal entity) in Statistics Sweden's business register (SCB) by organisation number. " +
        "Returns name, postal address, municipality, industry codes (SNI), size class, turnover class, ownership, " +
        "the phone and e-mail registered at SCB, and marketing blocks. " +
        "Respect the blocks: never suggest calling a number for sales if phoneBlocked is true, " +
        "or sending advertising if advertisingBlocked is true. " +
        "SCB's phone numbers can be outdated, so confirm them with find_phone_numbers on the company's own website.")]
    public async Task<CompanyInfo> GetCompany(
        [Description("Swedish organisation number, e.g. 556012-5790. The dash is optional.")] string orgNumber,
        CancellationToken cancellationToken)
    {
        if (!OrgNumber.TryParse(orgNumber, out var org, out var error))
        {
            throw new McpException(error);
        }

        try
        {
            var record = await scb.GetCompanyAsync(org, cancellationToken)
                ?? throw new McpException($"SCB has no legal entity with organisation number {org}.");
            var codes = await scb.GetCodeBookAsync(cancellationToken);
            return CompanyInfo.From(org, record, codes);
        }
        catch (ScbApiException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}

public sealed record CompanyInfo(
    string OrgNumber,
    string? Name,
    string? Status,
    string? LegalForm,
    PostalAddress? PostalAddress,
    string? Municipality,
    string? County,
    string? Employees,
    string? Turnover,
    int? TurnoverYear,
    string? Ownership,
    IReadOnlyList<Industry> Industries,
    int? Workplaces,
    DateOnly? Registered,
    Contact Contact,
    MarketingBlocks Blocks,
    IReadOnlyList<string> Notes)
{
    private static readonly TimeZoneInfo Stockholm = FindStockholmTimeZone();

    public static CompanyInfo From(OrgNumber org, ScbCompanyRecord record, ScbCodeBook codes)
    {
        var blocks = new MarketingBlocks(
            PhoneBlocked: codes.IsBlocked(ScbCodeBook.PhoneBlock, record.TelefonSparrTyp),
            PhoneBlock: codes.Describe(ScbCodeBook.PhoneBlock, record.TelefonSparrTyp),
            AdvertisingBlocked: codes.IsBlocked(ScbCodeBook.AdvertisingBlock, record.ReklamSparrTyp),
            EmailBlocked: codes.IsBlocked(ScbCodeBook.EmailBlock, record.EpostSparrTyp));

        var notes = new List<string>();
        if (blocks.PhoneBlocked != false)
        {
            notes.Add("Telemarketing block (telefonspärr) or unknown status: do not use the phone number for sales calls.");
        }
        if (blocks.AdvertisingBlocked != false)
        {
            notes.Add("Advertising block (reklamspärr) or unknown status: do not send addressed direct marketing.");
        }
        notes.Add("Phone and e-mail come from SCB and may be outdated. Confirm them on the company's own website before use.");

        var address = record.PostAdress is { } a
            ? new PostalAddress(Blank(a.GatuAdress), Blank(a.CoAdress), Blank(a.PostNr), Blank(a.PostOrt))
            : null;

        var industries = (record.Naringsgrenar ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Naringsgren))
            .OrderBy(i => i.Rangordning)
            .Select(i => new Industry(i.Naringsgren!, codes.Describe(ScbCodeBook.Industry, i.Naringsgren), i.AndelProcent))
            .ToList();

        return new CompanyInfo(
            OrgNumber: org.Formatted,
            Name: Blank(record.Namn),
            Status: codes.Describe(ScbCodeBook.CompanyStatus, record.FtgStat),
            LegalForm: codes.Describe(ScbCodeBook.LegalForm, record.Jurform),
            PostalAddress: address,
            Municipality: codes.Describe(ScbCodeBook.Municipality, record.KommunSate),
            County: codes.Describe(ScbCodeBook.County, record.LanSate),
            Employees: codes.Describe(ScbCodeBook.Employees, record.AnstKl),
            Turnover: codes.Describe(ScbCodeBook.Turnover, record.OmsKl),
            TurnoverYear: record.OmsAr is > 0 ? record.OmsAr : null, // SCB uses 0 for "unknown".
            Ownership: codes.Describe(ScbCodeBook.Ownership, record.AgKat),
            Industries: industries,
            Workplaces: record.AeAnt,
            Registered: record.RegDat is { } registered ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(registered, Stockholm).DateTime) : null,
            Contact: new Contact(Blank(record.Tel), Blank(record.Epost), "SCB business register (not verified)"),
            Blocks: blocks,
            Notes: notes);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeZoneInfo FindStockholmTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }
}

public sealed record PostalAddress(string? Street, string? CareOf, string? PostalCode, string? City);

public sealed record Industry(string SniCode, string? Description, int? SharePercent);

public sealed record Contact(string? Phone, string? Email, string Source);

public sealed record MarketingBlocks(bool? PhoneBlocked, string? PhoneBlock, bool? AdvertisingBlocked, bool? EmailBlocked);
