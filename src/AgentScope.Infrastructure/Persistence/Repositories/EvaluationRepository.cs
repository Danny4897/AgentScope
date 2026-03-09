using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class EvaluationRepository : IEvaluationRepository
{
    private readonly AgentScopeDbContext _db;
    public EvaluationRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<Unit>> SaveAsync(Evaluation eval, CancellationToken ct = default)
    {
        var existing = await _db.Evaluations.FirstOrDefaultAsync(e => e.TraceId == eval.TraceId, ct);
        if (existing is null)
            await _db.Evaluations.AddAsync(eval, ct);
        else
            existing.Update(eval.Score, eval.Label, eval.Note, eval.ReviewedBy);
        await _db.SaveChangesAsync(ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Evaluation?>> GetByTraceAsync(Guid traceId, CancellationToken ct = default)
    {
        var eval = await _db.Evaluations.FirstOrDefaultAsync(e => e.TraceId == traceId, ct);
        return Result<Evaluation?>.Success(eval);
    }

    public async Task<Result<EvalAggregate>> GetAggregateAsync(Guid applicationId, CancellationToken ct = default)
    {
        var scores = await _db.Evaluations
            .Where(e => e.ApplicationId == applicationId)
            .Select(e => e.Score)
            .ToListAsync(ct);

        int    total      = scores.Count;
        int    thumbsUp   = scores.Count(s => s == EvalScore.ThumbsUp);
        int    thumbsDown = scores.Count(s => s == EvalScore.ThumbsDown);
        int    neutral    = scores.Count(s => s == EvalScore.Neutral);
        double rate       = total > 0 ? thumbsUp * 100.0 / total : 0.0;

        return Result<EvalAggregate>.Success(new EvalAggregate(total, thumbsUp, thumbsDown, neutral, rate));
    }
}
