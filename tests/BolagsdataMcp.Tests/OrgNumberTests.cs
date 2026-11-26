namespace BolagsdataMcp.Tests;

public class OrgNumberTests
{
    [Theory]
    [InlineData("556012-5790", "556012-5790")]
    [InlineData("5560125790", "556012-5790")]
    [InlineData("165560125790", "556012-5790")]
    [InlineData(" 556703-7485 ", "556703-7485")]
    public void Parses_valid_organisation_numbers(string input, string expected)
    {
        Assert.True(OrgNumber.TryParse(input, out var org, out _));
        Assert.Equal(expected, org.Formatted);
    }

    [Fact]
    public void Scb_form_has_the_legal_entity_prefix()
    {
        Assert.True(OrgNumber.TryParse("556012-5790", out var org, out _));
        Assert.Equal("165560125790", org.ScbForm);
    }

    [Theory]
    [InlineData("556012-5791")] // Wrong check digit.
    [InlineData("556012-579")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_input(string? input)
    {
        Assert.False(OrgNumber.TryParse(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("800101-1234")]
    [InlineData("198001011234")]
    public void Rejects_personal_identity_numbers(string input)
    {
        Assert.False(OrgNumber.TryParse(input, out _, out var error));
        Assert.Contains("personal identity number", error, StringComparison.OrdinalIgnoreCase);
    }
}
