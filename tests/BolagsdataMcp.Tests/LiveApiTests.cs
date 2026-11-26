using System.Text.Json;

namespace BolagsdataMcp.Tests;

/// <summary>
/// Calls the real APIs through the server. They need internet access, and get_company needs SCB_AFR_API_KEY.
/// Skip them with: dotnet test --filter "Category!=Live"
/// </summary>
[Trait("Category", "Live")]
public class LiveApiTests
{
    [Fact]
    public async Task Looks_up_a_company_at_scb()
    {
        await using var client = await TestServer.StartAsync();

        var result = await client.CallToolAsync("get_company", new Dictionary<string, object?> { ["orgNumber"] = "556012-5790" });

        Assert.True(result.IsError != true, TestServer.TextOf(result));
        using var json = JsonDocument.Parse(TestServer.TextOf(result));
        var company = json.RootElement;
        Assert.Equal("AKTIEBOLAGET VOLVO", company.GetProperty("name").GetString());
        Assert.Equal("Västra Götaland", company.GetProperty("county").GetString());
        Assert.NotEmpty(company.GetProperty("industries").EnumerateArray());
    }

    [Fact]
    public async Task Finds_a_phone_number_on_a_company_website()
    {
        await using var client = await TestServer.StartAsync();

        var result = await client.CallToolAsync("find_phone_numbers", new Dictionary<string, object?> { ["website"] = "scb.se" });

        Assert.True(result.IsError != true, TestServer.TextOf(result));
        using var json = JsonDocument.Parse(TestServer.TextOf(result));
        Assert.NotEmpty(json.RootElement.GetProperty("pagesRead").EnumerateArray());
        Assert.NotEmpty(json.RootElement.GetProperty("numbers").EnumerateArray());
    }
}
