using MonadicSharp;

namespace AgentScope.Domain.Services;

public interface IStripeService
{
    Task<Result<string>> CreateCheckoutSessionAsync(Guid userId, string priceId, CancellationToken ct = default);
    Task<Result<Unit>> HandleWebhookEventAsync(string json, string signature, CancellationToken ct = default);
}
