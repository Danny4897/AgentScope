using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

/// <summary>Repository port for <see cref="Trace"/> aggregate (with Spans and Events).</summary>
public interface ITraceRepository
{
    Task<Result<Trace>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns a page of traces for a given application, newest first.</summary>
    Task<Result<IReadOnlyList<Trace>>> GetPageAsync(
        Guid applicationId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<Unit>> AddAsync(Trace trace, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(Trace trace, CancellationToken ct = default);

    /// <summary>Deletes all traces older than the given retention cutoff.</summary>
    Task<Result<int>> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>Counts spans within the given time window for quota enforcement.</summary>
    Task<Result<long>> CountSpansByApplicationAsync(
        Guid applicationId,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default);
}
