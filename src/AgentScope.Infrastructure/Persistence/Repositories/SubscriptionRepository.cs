using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AgentScopeDbContext _db;

    public SubscriptionRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);
        return sub is null
            ? Result<Subscription>.Failure(DomainErrors.Subscriptions.NotFound(userId))
            : Result<Subscription>.Success(sub);
    }

    public async Task<Result<Subscription>> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);
        return sub is null
            ? Result<Subscription>.Failure(DomainErrors.Subscriptions.NotFound(Guid.Empty))
            : Result<Subscription>.Success(sub);
    }

    public async Task<Result<Unit>> AddAsync(Subscription subscription, CancellationToken ct = default)
    {
        await _db.Subscriptions.AddAsync(subscription, ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public Task<Result<Unit>> UpdateAsync(Subscription subscription, CancellationToken ct = default)
    {
        _db.Subscriptions.Update(subscription);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }
}
