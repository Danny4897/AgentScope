namespace AgentScope.Domain.Entities;

/// <summary>
/// An OTLP span within a Trace. Maps 1:1 to an OpenTelemetry span.
/// Represents a single agent step, LLM call, tool invocation, or sub-pipeline.
/// </summary>
public sealed class Span
{
    public Guid Id { get; private set; }
    public Guid TraceId { get; private set; }
    public string SpanId { get; private set; }          // 8-byte hex (OTLP)
    public string? ParentSpanId { get; private set; }
    public string Name { get; private set; }
    public SpanKind Kind { get; private set; }
    public SpanStatus Status { get; private set; }
    public string? StatusMessage { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public long DurationMs => (long)(EndedAt - StartedAt).TotalMilliseconds;

    /// <summary>OTLP attributes serialised as JSON (e.g. model name, token counts).</summary>
    public string AttributesJson { get; private set; } = "{}";

    // Navigation
    public IReadOnlyCollection<AgentEvent> Events => _events.AsReadOnly();
    private readonly List<AgentEvent> _events = [];

    private Span() { SpanId = null!; Name = null!; AttributesJson = null!; }

    public static Span Create(
        Guid traceId,
        string spanId,
        string? parentSpanId,
        string name,
        SpanKind kind,
        SpanStatus status,
        string? statusMessage,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string attributesJson = "{}") =>
        new()
        {
            Id             = Guid.NewGuid(),
            TraceId        = traceId,
            SpanId         = spanId,
            ParentSpanId   = parentSpanId,
            Name           = name,
            Kind           = kind,
            Status         = status,
            StatusMessage  = statusMessage,
            StartedAt      = startedAt,
            EndedAt        = endedAt,
            AttributesJson = attributesJson,
        };
}

public enum SpanKind  { Internal, Server, Client, Producer, Consumer }
public enum SpanStatus { Unset, Ok, Error }
