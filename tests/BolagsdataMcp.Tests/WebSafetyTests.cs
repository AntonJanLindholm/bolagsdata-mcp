using System.Net;
using BolagsdataMcp.Web;

namespace BolagsdataMcp.Tests;

public class WebSafetyTests
{
    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)] // Cloud metadata endpoint.
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2a00:1450:400f:80c::200e", true)]
    public void Only_public_addresses_are_allowed(string address, bool expected)
    {
        Assert.Equal(expected, PublicNetwork.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("example.se", "https://example.se/")]
    [InlineData("www.example.se", "https://www.example.se/")]
    [InlineData("http://example.se/kontakt?x=1", "https://example.se/")]
    [InlineData("https://räksmörgås.se", "https://xn--rksmrgs-5wao1o.se/")]
    public void Normalises_website_input_to_the_site_root(string input, string expected)
    {
        Assert.Equal(expected, WebsiteFetcher.ToSiteRoot(input)?.ToString());
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("http://192.168.1.1/admin")]
    [InlineData("localhost")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.se")]
    [InlineData("https://user:secret@example.se")]
    public void Rejects_website_input_that_is_not_a_public_host_name(string input)
    {
        Assert.Null(WebsiteFetcher.ToSiteRoot(input));
    }
}
