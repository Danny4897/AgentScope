using System.Text.Json.Serialization;

namespace AgentScope.Api.Models.Otlp;

/// <summary>OTLP AnyValue — only the scalar cases we need for span attributes.</summary>
public sealed class OtlpAnyValue
{
    [JsonPropertyName("stringValue")]  public string?  StringValue  { get; init; }
    [JsonPropertyName("intValue")]     public long?    IntValue     { get; init; }
    [JsonPropertyName("doubleValue")]  public double?  DoubleValue  { get; init; }
    [JsonPropertyName("boolValue")]    public bool?    BoolValue    { get; init; }

    public string ToDisplayString() =>
        StringValue ?? IntValue?.ToString() ?? DoubleValue?.ToString() ?? BoolValue?.ToString() ?? "";
}

public sealed class OtlpKeyValue
{
    [JsonPropertyName("key")]   public string        Key   { get; init; } = "";
    [JsonPropertyName("value")] public OtlpAnyValue? Value { get; init; }
}

public sealed class OtlpResource
{
    [JsonPropertyName("attributes")] public List<OtlpKeyValue> Attributes { get; init; } = [];
}

public sealed class OtlpInstrumentationScope
{
    [JsonPropertyName("name")]    public string Name    { get; init; } = "";
    [JsonPropertyName("version")] public string Version { get; init; } = "";
}
