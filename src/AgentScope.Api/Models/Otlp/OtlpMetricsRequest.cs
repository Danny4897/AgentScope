using System.Text.Json.Serialization;

namespace AgentScope.Api.Models.Otlp;

/// <summary>OTLP/HTTP JSON metrics export request body.</summary>
public sealed class OtlpMetricsRequest
{
    [JsonPropertyName("resourceMetrics")] public List<OtlpResourceMetrics> ResourceMetrics { get; init; } = [];
}

public sealed class OtlpResourceMetrics
{
    [JsonPropertyName("resource")]      public OtlpResource?          Resource      { get; init; }
    [JsonPropertyName("scopeMetrics")] public List<OtlpScopeMetrics> ScopeMetrics { get; init; } = [];
}

public sealed class OtlpScopeMetrics
{
    [JsonPropertyName("scope")]   public OtlpInstrumentationScope? Scope   { get; init; }
    [JsonPropertyName("metrics")] public List<OtlpMetric>          Metrics { get; init; } = [];
}

public sealed class OtlpMetric
{
    [JsonPropertyName("name")]      public string           Name      { get; init; } = "";
    [JsonPropertyName("unit")]      public string           Unit      { get; init; } = "";
    [JsonPropertyName("gauge")]     public OtlpGauge?       Gauge     { get; init; }
    [JsonPropertyName("sum")]       public OtlpSum?         Sum       { get; init; }
    [JsonPropertyName("histogram")] public OtlpHistogram?   Histogram { get; init; }
}

public sealed class OtlpGauge
{
    [JsonPropertyName("dataPoints")] public List<OtlpNumberDataPoint> DataPoints { get; init; } = [];
}

public sealed class OtlpSum
{
    [JsonPropertyName("dataPoints")]              public List<OtlpNumberDataPoint> DataPoints { get; init; } = [];
    [JsonPropertyName("aggregationTemporality")] public int  AggregationTemporality { get; init; }
    [JsonPropertyName("isMonotonic")]            public bool IsMonotonic            { get; init; }
}

public sealed class OtlpHistogram
{
    [JsonPropertyName("dataPoints")] public List<OtlpHistogramDataPoint> DataPoints { get; init; } = [];
}

public sealed class OtlpNumberDataPoint
{
    [JsonPropertyName("attributes")]        public List<OtlpKeyValue> Attributes       { get; init; } = [];
    [JsonPropertyName("timeUnixNano")]      public string             TimeUnixNano     { get; init; } = "0";
    [JsonPropertyName("startTimeUnixNano")] public string             StartTimeUnixNano { get; init; } = "0";
    [JsonPropertyName("asDouble")]          public double?            AsDouble         { get; init; }
    [JsonPropertyName("asInt")]             public long?              AsInt            { get; init; }

    public double GetValue() => AsDouble ?? AsInt ?? 0;
}

public sealed class OtlpHistogramDataPoint
{
    [JsonPropertyName("attributes")]        public List<OtlpKeyValue> Attributes       { get; init; } = [];
    [JsonPropertyName("timeUnixNano")]      public string             TimeUnixNano     { get; init; } = "0";
    [JsonPropertyName("startTimeUnixNano")] public string             StartTimeUnixNano { get; init; } = "0";
    [JsonPropertyName("count")]             public long               Count            { get; init; }
    [JsonPropertyName("sum")]               public double?            Sum              { get; init; }
    [JsonPropertyName("min")]               public double?            Min              { get; init; }
    [JsonPropertyName("max")]               public double?            Max              { get; init; }
}
