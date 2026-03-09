using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

public record EvalAggregate(int Total, int ThumbsUp, int ThumbsDown, int Neutral, double PositiveRate);

public interface IEvaluationRepository
{
    Task<Result<Unit>>          SaveAsync(Evaluation eval, CancellationToken ct = default);
    Task<Result<Evaluation?>>   GetByTraceAsync(Guid traceId, CancellationToken ct = default);
    Task<Result<EvalAggregate>> GetAggregateAsync(Guid applicationId, CancellationToken ct = default);
}
