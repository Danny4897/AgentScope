namespace AgentScope.Domain.Entities;

/// <summary>
/// An OTLP span event — a timestamped log line within a Span.
/// Used by MonadicSharp.Agents to record agent decisions, Result failures,
/// CircuitBreaker transitions, and other structured events.
/// </summary>
public sealed class AgentEvent
{
    public Guid Id { get; private set; }
    public Guid SpanId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>OTLP event attributes serialised as JSON.</summary>
    public string AttributesJson { get; private set; } = "{}";

    private AgentEvent() { Name = null!; AttributesJson = null!; }

    public static AgentEvent Create(
        Guid spanId,
        string name,
        DateTimeOffset occurredAt,
        string attributesJson = "{}") =>
        new()
        {
            Id             = Guid.NewGuid(),
            SpanId         = spanId,
            Name           = name,
            OccurredAt     = occurredAt,
            AttributesJson = attributesJson,
        };
}
