using BolagsdataMcp.Scb;
using BolagsdataMcp.Tools;
using BolagsdataMcp.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol, so every log line must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddHttpClient(ScbAfrClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://apiafr.scb.se/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpClient(WebsiteFetcher.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 2_000_000;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; bolagsdata-mcp/0.1)");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
})
.ConfigurePrimaryHttpMessageHandler(PublicNetwork.CreateHandler);

builder.Services.AddSingleton<ScbAfrClient>();
builder.Services.AddSingleton<WebsiteFetcher>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "bolagsdata", Version = "0.1.0" };
        options.ServerInstructions =
            "Tools for Swedish company data: Statistics Sweden's business register (SCB) " +
            "and contact details on a company's own website. " +
            "Treat a phone number as confirmed only when find_phone_numbers found it in the visible text " +
            "of the company's own website, and respect SCB's telemarketing and advertising blocks.";
    })
    .WithStdioServerTransport()
    .WithTools<CompanyTools>()
    .WithTools<WebsiteTools>();

await builder.Build().RunAsync();
