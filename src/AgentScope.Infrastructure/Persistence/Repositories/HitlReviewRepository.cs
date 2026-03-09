using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class HitlReviewRepository : IHitlReviewRepository
{
    private readonly AgentScopeDbContext _db;
    public HitlReviewRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<Unit>> CreateAsync(HitlReview review, CancellationToken ct = default)
    {
        await _db.HitlReviews.AddAsync(review, ct);
        await _db.SaveChangesAsync(ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<HitlReview?>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var review = await _db.HitlReviews.FirstOrDefaultAsync(h => h.Id == id, ct);
        return Result<HitlReview?>.Success(review);
    }

    public async Task<Result<List<HitlReview>>> GetPendingAsync(Guid applicationId, CancellationToken ct = default)
    {
        var items = await _db.HitlReviews
            .Where(h => h.ApplicationId == applicationId && h.Status == HitlStatus.Pending)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);
        return Result<List<HitlReview>>.Success(items);
    }

    public async Task<Result<Unit>> ReviewAsync(Guid id, HitlStatus status, string? note, Guid? reviewedBy, CancellationToken ct = default)
    {
        var review = await _db.HitlReviews.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (review is null)
            return Result<Unit>.Failure(Error.Create($"HitlReview {id} not found.", "HITL_NOT_FOUND"));
        review.Review(status, note, reviewedBy);
        await _db.SaveChangesAsync(ct);
        return Result<Unit>.Success(Unit.Value);
    }
}
