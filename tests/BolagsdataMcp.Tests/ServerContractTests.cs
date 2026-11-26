using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace BolagsdataMcp.Tests;

/// <summary>
/// Starts the real server over stdio, the way Claude Code does, and checks what an agent sees.
/// These tests make no calls to external APIs.
/// </summary>
public class ServerContractTests
{
    [Fact]
    public async Task Exposes_two_documented_read_only_tools()
    {
        await using var client = await TestServer.StartAsync();

        var tools = await client.ListToolsAsync();

        Assert.Equal(["find_phone_numbers", "get_company"], tools.Select(t => t.Name).Order());
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(tool.ProtocolTool.Annotations?.DestructiveHint);
        });
    }

    [Fact]
    public async Task Refuses_personal_identity_numbers()
    {
        await using var client = await TestServer.StartAsync();

        var result = await client.CallToolAsync("get_company", new Dictionary<string, object?> { ["orgNumber"] = "800101-1234" });

        Assert.True(result.IsError);
        Assert.Contains("personal identity number", TestServer.TextOf(result));
    }

    [Fact]
    public async Task Refuses_to_fetch_private_addresses()
    {
        await using var client = await TestServer.StartAsync();

        var result = await client.CallToolAsync("find_phone_numbers", new Dictionary<string, object?> { ["website"] = "http://169.254.169.254/latest/meta-data" });

        Assert.True(result.IsError);
        Assert.Contains("public website", TestServer.TextOf(result));
    }
}

internal static class TestServer
{
    /// <summary>The server DLL is copied to the test output folder through the project reference.</summary>
    public static Task<McpClient> StartAsync() =>
        McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "bolagsdata",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "BolagsdataMcp.dll")],
        }));

    public static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
