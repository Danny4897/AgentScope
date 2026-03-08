using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class TraceRepository : ITraceRepository
{
    private readonly AgentScopeDbContext _db;

    public TraceRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<Trace>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var trace = await _db.Traces
            .Include(t => t.Spans)
                .ThenInclude(s => s.Events)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return trace is null
            ? Result<Trace>.Failure(DomainErrors.Traces.NotFound(id))
            : Result<Trace>.Success(trace);
    }

    public async Task<Result<IReadOnlyList<Trace>>> GetPageAsync(
        Guid applicationId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var traces = await _db.Traces
            .Where(t => t.ApplicationId == applicationId)
            .OrderByDescending(t => t.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<IReadOnlyList<Trace>>.Success(traces);
    }

    public async Task<Result<Unit>> AddAsync(Trace trace, CancellationToken ct = default)
    {
        await _db.Traces.AddAsync(trace, ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public Task<Result<Unit>> UpdateAsync(Trace trace, CancellationToken ct = default)
    {
        _db.Traces.Update(trace);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    public async Task<Result<int>> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var deleted = await _db.Traces
            .Where(t => t.StartedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        return Result<int>.Success(deleted);
    }

    public async Task<Result<long>> CountSpansByApplicationAsync(
        Guid applicationId,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken ct = default)
    {
        var count = await _db.Traces
            .Where(t => t.ApplicationId == applicationId && t.StartedAt >= fromTime && t.StartedAt <= toTime)
            .SelectMany(t => t.Spans)
            .LongCountAsync(ct);

        return Result<long>.Success(count);
    }
}
