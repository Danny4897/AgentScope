using System.Text.Json.Serialization;

namespace AgentScope.Api.Models.Otlp;

/// <summary>OTLP/HTTP JSON trace export request body.</summary>
public sealed class OtlpTraceRequest
{
    [JsonPropertyName("resourceSpans")] public List<OtlpResourceSpans> ResourceSpans { get; init; } = [];
}

public sealed class OtlpResourceSpans
{
    [JsonPropertyName("resource")]   public OtlpResource?       Resource   { get; init; }
    [JsonPropertyName("scopeSpans")] public List<OtlpScopeSpan> ScopeSpans { get; init; } = [];
}

public sealed class OtlpScopeSpan
{
    [JsonPropertyName("scope")] public OtlpInstrumentationScope? Scope { get; init; }
    [JsonPropertyName("spans")] public List<OtlpSpan>            Spans { get; init; } = [];
}

public sealed class OtlpSpan
{
    [JsonPropertyName("traceId")]           public string              TraceId          { get; init; } = "";
    [JsonPropertyName("spanId")]            public string              SpanId           { get; init; } = "";
    [JsonPropertyName("parentSpanId")]      public string?             ParentSpanId     { get; init; }
    [JsonPropertyName("name")]              public string              Name             { get; init; } = "";
    [JsonPropertyName("kind")]              public int                 Kind             { get; init; }
    [JsonPropertyName("startTimeUnixNano")] public string              StartTimeUnixNano { get; init; } = "0";
    [JsonPropertyName("endTimeUnixNano")]   public string              EndTimeUnixNano   { get; init; } = "0";
    [JsonPropertyName("attributes")]        public List<OtlpKeyValue>  Attributes       { get; init; } = [];
    [JsonPropertyName("status")]            public OtlpSpanStatus?     Status           { get; init; }
}

public sealed class OtlpSpanStatus
{
    /// <summary>0=UNSET, 1=OK, 2=ERROR</summary>
    [JsonPropertyName("code")]    public int    Code    { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = "";
}
