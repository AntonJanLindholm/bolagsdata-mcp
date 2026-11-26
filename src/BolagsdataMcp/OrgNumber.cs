namespace BolagsdataMcp;

/// <summary>
/// A Swedish organisation number for a legal entity (juridisk person), for example 556012-5790.
/// </summary>
/// <remarks>
/// Sole traders use their personal identity number as organisation number. Those are rejected on purpose,
/// so the server never looks up or returns data about private individuals.
/// </remarks>
public readonly record struct OrgNumber
{
    private OrgNumber(string digits) => Digits = digits;

    /// <summary>The ten digits without separator, e.g. 5560125790.</summary>
    public string Digits { get; }

    /// <summary>The usual written form, e.g. 556012-5790.</summary>
    public string Formatted => $"{Digits[..6]}-{Digits[6..]}";

    /// <summary>SCB's twelve-digit form with the prefix 16 that marks a legal entity.</summary>
    public string ScbForm => "16" + Digits;

    public override string ToString() => Formatted;

    public static bool TryParse(string? input, out OrgNumber value, out string error)
    {
        value = default;
        var digits = new string((input ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 12)
        {
            if (!digits.StartsWith("16", StringComparison.Ordinal))
            {
                error = "A 12-digit number must start with 16 (legal entity). Personal identity numbers are not supported.";
                return false;
            }
            digits = digits[2..];
        }

        if (digits.Length != 10)
        {
            error = $"Expected 10 digits (NNNNNN-NNNN) but got {digits.Length}.";
            return false;
        }

        // For legal entities the third digit is always 2 or higher; for personal identity numbers it is a month (0 or 1).
        if (digits[2] < '2')
        {
            error = "This looks like a personal identity number (sole trader). Only legal entities are supported, to avoid handling personal data.";
            return false;
        }

        if (!HasValidCheckDigit(digits))
        {
            error = "The check digit is wrong. Look for a typo in the number.";
            return false;
        }

        value = new OrgNumber(digits);
        error = string.Empty;
        return true;
    }

    /// <summary>Luhn (mod 10) check of the last digit, as used for Swedish organisation numbers.</summary>
    private static bool HasValidCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var product = (digits[i] - '0') * (i % 2 == 0 ? 2 : 1);
            sum += product > 9 ? product - 9 : product;
        }
        return (10 - sum % 10) % 10 == digits[9] - '0';
    }
}
