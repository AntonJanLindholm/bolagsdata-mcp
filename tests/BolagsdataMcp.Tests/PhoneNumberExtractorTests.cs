using BolagsdataMcp.Web;

namespace BolagsdataMcp.Tests;

public class PhoneNumberExtractorTests
{
    [Theory]
    [InlineData("08-123 45 67", "+4681234567")]
    [InlineData("08-12 34 56", "+468123456")]
    [InlineData("+46 (0)8-123 45 67", "+4681234567")]
    [InlineData("+46 8 123 45 67", "+4681234567")]
    [InlineData("0046 8 123 45 67", "+4681234567")]
    [InlineData("070-123 45 67", "+46701234567")]
    [InlineData("031-66 00 00", "+4631660000")]
    public void Converts_swedish_numbers_to_e164(string written, string expected)
    {
        Assert.Equal(expected, PhoneNumberExtractor.ToE164(written));
    }

    [Theory]
    [InlineData("+45 33 12 34 56")] // Danish number.
    [InlineData("08-12")]
    [InlineData("556012-5790")] // Organisation number.
    public void Ignores_what_is_not_a_swedish_phone_number(string written)
    {
        Assert.Null(PhoneNumberExtractor.ToE164(written));
    }

    [Fact]
    public void Finds_a_number_in_visible_text()
    {
        const string html = "<html><head><title>Exempel AB</title></head><body><p>Ring oss: 08-123 45 67</p></body></html>";

        var match = Assert.Single(PhoneNumberExtractor.Extract(html));

        Assert.Equal("+4681234567", match.E164);
        Assert.Equal("08-123 45 67", match.AsWritten);
        Assert.Equal(PhoneSource.VisibleText, match.Source);
    }

    [Fact]
    public void Finds_a_tel_link()
    {
        const string html = "<body><a href=\"tel:+4681234567\">Ring oss</a></body>";

        var match = Assert.Single(PhoneNumberExtractor.Extract(html));

        Assert.Equal("+4681234567", match.E164);
        Assert.Equal(PhoneSource.TelLink, match.Source);
    }

    [Fact]
    public void Ignores_numbers_hidden_in_metadata_scripts_and_comments()
    {
        const string html = """
            <html><head>
              <meta name="description" content="Ring 08-765 43 21">
              <script type="application/ld+json">{"@type":"Organization","telephone":"08-765 43 21"}</script>
            </head><body>
              <script>var phone = '08-111 22 33';</script>
              <!-- old number 08-222 33 44 -->
              <p>Välkommen!</p>
            </body></html>
            """;

        Assert.Empty(PhoneNumberExtractor.Extract(html));
    }

    [Fact]
    public void Ignores_opening_hours_postal_codes_and_organisation_numbers()
    {
        const string html = "<p>Öppet 08:00-17:00. Box 123, 114 55 Stockholm. Org.nr 556012-5790</p>";

        Assert.Empty(PhoneNumberExtractor.Extract(html));
    }

    [Fact]
    public void Reads_the_page_title()
    {
        Assert.Equal("Exempel AB – Kontakt", PhoneNumberExtractor.GetTitle("<title>\n Exempel AB &ndash; Kontakt </title>"));
    }
}
