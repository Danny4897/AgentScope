using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

/// <summary>Repository port for <see cref="MetricPoint"/> aggregates.</summary>
public interface IMetricRepository
{
    Task<Result<IReadOnlyList<MetricPoint>>> GetByApplicationAsync(
        Guid applicationId,
        string timeWindow,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default);

    Task<Result<Unit>> AddRangeAsync(IEnumerable<MetricPoint> points, CancellationToken ct = default);

    Task<Result<long>> CountByApplicationAsync(
        Guid applicationId,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default);
}
