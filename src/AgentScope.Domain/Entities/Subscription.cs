namespace AgentScope.Domain.Entities;

/// <summary>
/// Stripe subscription record for a user.
/// Mirrors the Stripe Subscription object enough to enforce tier limits
/// without real-time Stripe API calls on every request.
/// </summary>
public sealed class Subscription
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string StripeSubscriptionId { get; private set; }
    public string StripePriceId { get; private set; }
    public SubscriptionTier Tier { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? StripeCustomerId { get; private set; }

    private Subscription() { StripeSubscriptionId = null!; StripePriceId = null!; }

    public void SetStripeCustomerId(string customerId) => StripeCustomerId = customerId;

    public static Subscription Create(
        Guid userId,
        string stripeSubscriptionId,
        string stripePriceId,
        SubscriptionTier tier,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd) =>
        new()
        {
            Id                     = Guid.NewGuid(),
            UserId                 = userId,
            StripeSubscriptionId   = stripeSubscriptionId,
            StripePriceId          = stripePriceId,
            Tier                   = tier,
            Status                 = SubscriptionStatus.Active,
            CurrentPeriodStart     = periodStart,
            CurrentPeriodEnd       = periodEnd,
            CreatedAt              = DateTimeOffset.UtcNow,
            UpdatedAt              = DateTimeOffset.UtcNow,
        };

    public void Cancel()
    {
        Status    = SubscriptionStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Renew(DateTimeOffset newPeriodStart, DateTimeOffset newPeriodEnd)
    {
        CurrentPeriodStart = newPeriodStart;
        CurrentPeriodEnd   = newPeriodEnd;
        Status             = SubscriptionStatus.Active;
        UpdatedAt          = DateTimeOffset.UtcNow;
    }
}

public enum SubscriptionStatus { Active, PastDue, Cancelled, Trialing }
