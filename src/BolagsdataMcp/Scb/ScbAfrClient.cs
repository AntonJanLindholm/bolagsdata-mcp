using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BolagsdataMcp.Scb;

/// <summary>
/// Client for SCB's business register API (Allmänna företagsregistret, AFR).
/// Docs: https://apiafr.scb.se/scalar/ – requires a free personal API key from https://registreraafr.scb.se.
/// </summary>
public sealed class ScbAfrClient(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "scb-afr";
    public const string ApiKeyVariable = "SCB_AFR_API_KEY";

    // SCB allows at most five calls per second.
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private readonly SemaphoreSlim _codeBookGate = new(1, 1);
    private DateTimeOffset _lastCall = DateTimeOffset.MinValue;
    private ScbCodeBook? _codeBook;

    /// <summary>Returns the full register entry, or null when SCB has no legal entity with that number.</summary>
    public async Task<ScbCompanyRecord?> GetCompanyAsync(OrgNumber orgNumber, CancellationToken cancellationToken)
    {
        using var response = await SendAsync($"juridiskaenheter/{orgNumber.ScbForm}/full", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<ScbCompanyRecord>(JsonOptions, cancellationToken);
    }

    /// <summary>Loads SCB's code tables once and keeps them for the lifetime of the server.</summary>
    public async Task<ScbCodeBook> GetCodeBookAsync(CancellationToken cancellationToken)
    {
        if (_codeBook is not null)
        {
            return _codeBook;
        }

        await _codeBookGate.WaitAsync(cancellationToken);
        try
        {
            if (_codeBook is null)
            {
                var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>();
                foreach (var table in ScbCodeBook.Tables)
                {
                    tables[table] = await LoadCodeTableAsync(table, cancellationToken);
                }
                _codeBook = new ScbCodeBook(tables);
            }
            return _codeBook;
        }
        finally
        {
            _codeBookGate.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadCodeTableAsync(string table, CancellationToken cancellationToken)
    {
        using var response = await SendAsync($"kodtabeller/{table}", cancellationToken);
        EnsureSuccess(response);
        var entries = await response.Content.ReadFromJsonAsync<List<ScbCodeEntry>>(JsonOptions, cancellationToken) ?? [];
        return entries
            .GroupBy(entry => entry.Code)
            .ToDictionary(group => group.Key, group => group.First().Text.Trim());
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
    {
        var apiKey = ReadApiKey() ?? throw new ScbApiException(
            $"SCB API key is missing. Get a free key at https://registreraafr.scb.se and store it in the environment variable {ApiKeyVariable}.");

        await _rateGate.WaitAsync(cancellationToken);
        try
        {
            var wait = _lastCall + MinimumInterval - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
            _lastCall = DateTimeOffset.UtcNow;
        }
        finally
        {
            _rateGate.Release();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-API-Key", apiKey);
        return await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new ScbApiException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"SCB rejected the API key (HTTP {(int)response.StatusCode}). Check {ApiKeyVariable}.",
            HttpStatusCode.TooManyRequests => "SCB's rate limit was hit. Try again in a few seconds.",
            _ => $"SCB answered with HTTP {(int)response.StatusCode}.",
        });
    }

    private static string? ReadApiKey()
    {
        var key = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (string.IsNullOrWhiteSpace(key) && OperatingSystem.IsWindows())
        {
            // Lets the server find a key stored as a Windows user variable even if the client started it without that variable.
            key = Environment.GetEnvironmentVariable(ApiKeyVariable, EnvironmentVariableTarget.User);
        }
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}

public sealed class ScbApiException(string message) : Exception(message);
