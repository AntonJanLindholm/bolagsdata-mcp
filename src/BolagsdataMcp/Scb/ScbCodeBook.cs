namespace BolagsdataMcp.Scb;

/// <summary>SCB's code tables, used to turn codes such as anstKl = "6" into "50-99 anställda".</summary>
public sealed class ScbCodeBook(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables)
{
    public const string Employees = "anstklkoder";
    public const string Turnover = "omsklkoder";
    public const string Ownership = "agarkontrollkoder";
    public const string LegalForm = "jurformkoder";
    public const string CompanyStatus = "ftgstatkoder";
    public const string Industry = "naringsgrenkoder";
    public const string Municipality = "kommunkoder";
    public const string County = "lankoder";
    public const string PhoneBlock = "telefonsparrtypkoder";
    public const string AdvertisingBlock = "reklamsparrtypkoder";
    public const string EmailBlock = "epostsparrtypkoder";

    public static readonly string[] Tables =
    [
        Employees, Turnover, Ownership, LegalForm, CompanyStatus, Industry,
        Municipality, County, PhoneBlock, AdvertisingBlock, EmailBlock,
    ];

    /// <summary>The plain-text meaning of a code, or the code itself when the table does not know it.</summary>
    public string? Describe(string table, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        return tables.TryGetValue(table, out var entries) && entries.TryGetValue(code, out var text) ? text : code;
    }

    /// <summary>
    /// Whether a block (spärr) applies. A code only counts as "no block" when SCB's own text says so
    /// ("Ej telefonnummerspärrat", "Tar emot reklam"). Unknown codes count as blocked, to stay on the safe side.
    /// Returns null when SCB has no value at all.
    /// </summary>
    public bool? IsBlocked(string table, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        if (!tables.TryGetValue(table, out var entries) || !entries.TryGetValue(code, out var text))
        {
            return true;
        }
        var meansNoBlock = text.StartsWith("Ej ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Tar emot", StringComparison.OrdinalIgnoreCase);
        return !meansNoBlock;
    }
}
