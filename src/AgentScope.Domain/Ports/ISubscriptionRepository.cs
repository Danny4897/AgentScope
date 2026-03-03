using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

/// <summary>Repository port for <see cref="Subscription"/>.</summary>
public interface ISubscriptionRepository
{
    Task<Result<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Result<Subscription>> GetByStripeIdAsync(string stripeSubscriptionId, CancellationToken ct = default);
    Task<Result<Unit>> AddAsync(Subscription subscription, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(Subscription subscription, CancellationToken ct = default);
}
