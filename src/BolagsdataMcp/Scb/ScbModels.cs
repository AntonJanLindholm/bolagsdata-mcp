using System.Text.Json;
using System.Text.Json.Serialization;

namespace BolagsdataMcp.Scb;

// These types mirror SCB's JSON (Swedish field names). Tools map them to English output models.

public sealed class ScbCompanyRecord
{
    public string? OrgNr { get; init; }
    public string? Namn { get; init; }
    public ScbPostalAddress? PostAdress { get; init; }
    public string? KommunSate { get; init; }
    public string? LanSate { get; init; }
    public int? AeAnt { get; init; }

    [JsonConverter(typeof(CodeConverter))] public string? AnstKl { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? FtgStat { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? Jurform { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? ReklamSparrTyp { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? TelefonSparrTyp { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? EpostSparrTyp { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? OmsKl { get; init; }
    [JsonConverter(typeof(CodeConverter))] public string? AgKat { get; init; }

    public DateTimeOffset? RegDat { get; init; }
    public string? Tel { get; init; }
    public string? Epost { get; init; }
    public int? OmsAr { get; init; }
    public List<ScbIndustry>? Naringsgrenar { get; init; }
}

public sealed class ScbPostalAddress
{
    public string? GatuAdress { get; init; }
    public string? CoAdress { get; init; }
    public string? PostNr { get; init; }
    public string? PostOrt { get; init; }
}

public sealed class ScbIndustry
{
    public int Rangordning { get; init; }
    public string? Naringsgren { get; init; }
    public int? AndelProcent { get; init; }
}

public sealed class ScbCodeEntry
{
    [JsonPropertyName("kod")]
    [JsonConverter(typeof(CodeConverter))]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("klartext")]
    public string Text { get; init; } = string.Empty;
}

/// <summary>SCB sends some codes as numbers and others as strings. This reads both as strings.</summary>
internal sealed class CodeConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var number) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for an SCB code."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
