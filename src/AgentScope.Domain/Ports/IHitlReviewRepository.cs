using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

public interface IHitlReviewRepository
{
    Task<Result<Unit>>             CreateAsync(HitlReview review, CancellationToken ct = default);
    Task<Result<HitlReview?>>      GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<List<HitlReview>>> GetPendingAsync(Guid applicationId, CancellationToken ct = default);
    Task<Result<Unit>>             ReviewAsync(Guid id, HitlStatus status, string? note, Guid? reviewedBy, CancellationToken ct = default);
}
