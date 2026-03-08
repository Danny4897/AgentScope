using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class MetricRepository : IMetricRepository
{
    private readonly AgentScopeDbContext _db;

    public MetricRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<MetricPoint>>> GetByApplicationAsync(
        Guid applicationId,
        string timeWindow,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default)
    {
        var points = await _db.MetricPoints
            .Where(m => m.ApplicationId == applicationId
                     && m.TimeWindow == timeWindow
                     && m.WindowStart >= fromTime
                     && m.WindowStart <= toTime)
            .OrderBy(m => m.WindowStart)
            .ToListAsync(ct);

        return Result<IReadOnlyList<MetricPoint>>.Success(points);
    }

    public async Task<Result<Unit>> AddRangeAsync(IEnumerable<MetricPoint> points, CancellationToken ct = default)
    {
        await _db.MetricPoints.AddRangeAsync(points, ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<long>> CountByApplicationAsync(
        Guid applicationId,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default)
    {
        var count = await _db.MetricPoints
            .Where(m => m.ApplicationId == applicationId
                     && m.TimeWindow == "raw"
                     && m.WindowStart >= fromTime
                     && m.WindowStart <= toTime)
            .LongCountAsync(ct);

        return Result<long>.Success(count);
    }
}
