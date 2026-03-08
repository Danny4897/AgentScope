namespace AgentScope.Domain.Entities;

/// <summary>
/// An OTLP trace: a tree of Spans that represent one end-to-end pipeline run.
/// Received via POST /v1/traces and stored for the account's retention window.
/// </summary>
public sealed class Trace
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string TraceId { get; private set; }         // 16-byte hex (OTLP)
    public string RootSpanName { get; private set; }
    public TraceStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public long DurationMs => EndedAt.HasValue
        ? (long)(EndedAt.Value - StartedAt).TotalMilliseconds
        : 0;

    // Navigation
    public IReadOnlyCollection<Span> Spans => _spans.AsReadOnly();
    private readonly List<Span> _spans = [];

    private Trace() { TraceId = null!; RootSpanName = null!; }

    public static Trace Create(
        Guid applicationId,
        string traceId,
        string rootSpanName,
        DateTimeOffset startedAt) =>
        new()
        {
            Id            = Guid.NewGuid(),
            ApplicationId = applicationId,
            TraceId       = traceId,
            RootSpanName  = rootSpanName,
            Status        = TraceStatus.Running,
            StartedAt     = startedAt,
        };

    public void Complete(TraceStatus finalStatus, DateTimeOffset endedAt)
    {
        Status  = finalStatus;
        EndedAt = endedAt;
    }

    /// <summary>
    /// Attaches spans to this trace before persistence.
    /// Called by ingestion agents to populate the EF navigation property.
    /// </summary>
    public void AttachSpans(IEnumerable<Span> spans) => _spans.AddRange(spans);
}

public enum TraceStatus
{
    Running,
    Success,
    Failure,
    Timeout,
}
