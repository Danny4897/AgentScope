using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using AgentScope.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MonadicSharp;
using MonadicSharp.Persistence.Core;
using Stripe;
using Stripe.Checkout;

namespace AgentScope.Infrastructure.Services;

public sealed class StripeService : IStripeService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        ISubscriptionRepository subscriptions,
        IUserRepository users,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<StripeService> logger)
    {
        _subscriptions = subscriptions;
        _users         = users;
        _uow           = uow;
        _config        = config;
        _logger        = logger;

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is required.");
    }

    public async Task<Result<string>> CreateCheckoutSessionAsync(
        Guid userId, string priceId, CancellationToken ct = default)
    {
        try
        {
            var successUrl = _config["Stripe:SuccessUrl"] ?? "https://app.agentscope.dev/billing/success";
            var cancelUrl  = _config["Stripe:CancelUrl"]  ?? "https://app.agentscope.dev/billing/cancel";

            var options = new SessionCreateOptions
            {
                Mode      = "subscription",
                LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
                Metadata  = new Dictionary<string, string> { ["user_id"] = userId.ToString() },
                SuccessUrl = successUrl,
                CancelUrl  = cancelUrl,
            };

            var session = await new SessionService().CreateAsync(options, cancellationToken: ct);
            return Result<string>.Success(session.Url);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed for user {UserId}", userId);
            return Result<string>.Failure(DomainErrors.Stripe.CheckoutFailed(ex.Message));
        }
    }

    public async Task<Result<Unit>> HandleWebhookEventAsync(
        string json, string signature, CancellationToken ct = default)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? string.Empty;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Result<Unit>.Failure(DomainErrors.Stripe.WebhookVerificationFailed());
        }

        try
        {
            return stripeEvent.Type switch
            {
                "checkout.session.completed"      => await HandleCheckoutCompletedAsync(stripeEvent, ct),
                "customer.subscription.deleted"   => await HandleSubscriptionDeletedAsync(stripeEvent, ct),
                _                                 => Result<Unit>.Success(Unit.Value),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe event {EventType}", stripeEvent.Type);
            return Result<Unit>.Failure(DomainErrors.Stripe.EventProcessingFailed(stripeEvent.Type));
        }
    }

    private async Task<Result<Unit>> HandleCheckoutCompletedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Session session)
            return Result<Unit>.Failure(DomainErrors.Stripe.EventProcessingFailed(stripeEvent.Type));

        if (!session.Metadata.TryGetValue("user_id", out var userIdStr)
            || !Guid.TryParse(userIdStr, out var userId))
            return Result<Unit>.Failure(DomainErrors.Stripe.EventProcessingFailed(stripeEvent.Type));

        var userResult = await _users.GetByIdAsync(userId, ct);
        if (!userResult.IsSuccess)
            return Result<Unit>.Failure(userResult.Error!);

        var user = userResult.Value!;
        var tier = MapPriceIdToTier(session);

        var subscription = AgentScope.Domain.Entities.Subscription.Create(
            userId:               userId,
            stripeSubscriptionId: session.SubscriptionId ?? string.Empty,
            stripePriceId:        session.Subscription?.ToString() ?? string.Empty,
            tier:                 tier,
            periodStart:          DateTimeOffset.UtcNow,
            periodEnd:            DateTimeOffset.UtcNow.AddMonths(1));

        subscription.SetStripeCustomerId(session.CustomerId ?? string.Empty);
        user.UpgradeTier(tier);

        var addResult = await _subscriptions.AddAsync(subscription, ct);
        if (!addResult.IsSuccess) return addResult;

        var updateResult = await _users.UpdateAsync(user, ct);
        if (!updateResult.IsSuccess) return updateResult;

        await _uow.SaveChangesAsync(ct);
        return Result<Unit>.Success(Unit.Value);
    }

    private async Task<Result<Unit>> HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not global::Stripe.Subscription stripeSub)
            return Result<Unit>.Failure(DomainErrors.Stripe.EventProcessingFailed(stripeEvent.Type));

        var subResult = await _subscriptions.GetByStripeIdAsync(stripeSub.Id, ct);
        if (!subResult.IsSuccess)
            return Result<Unit>.Failure(subResult.Error!);

        var subscription = subResult.Value!;
        subscription.Cancel();

        var userResult = await _users.GetByIdAsync(subscription.UserId, ct);
        if (!userResult.IsSuccess)
            return Result<Unit>.Failure(userResult.Error!);

        userResult.Value!.UpgradeTier(SubscriptionTier.Free);

        var updateSubResult = await _subscriptions.UpdateAsync(subscription, ct);
        if (!updateSubResult.IsSuccess) return updateSubResult;

        var updateUserResult = await _users.UpdateAsync(userResult.Value!, ct);
        if (!updateUserResult.IsSuccess) return updateUserResult;

        await _uow.SaveChangesAsync(ct);
        return Result<Unit>.Success(Unit.Value);
    }

    private SubscriptionTier MapPriceIdToTier(Session session)
    {
        var priceId = session.LineItems?.Data?.Count > 0
            ? session.LineItems.Data[0].Price?.Id ?? string.Empty
            : string.Empty;

        if (priceId == _config["Stripe:EnterprisePriceId"]) return SubscriptionTier.Enterprise;
        if (priceId == _config["Stripe:BusinessPriceId"])   return SubscriptionTier.Business;
        if (priceId == _config["Stripe:ProPriceId"])        return SubscriptionTier.Pro;

        return SubscriptionTier.Pro;
    }
}
