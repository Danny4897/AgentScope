using AgentScope.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentScope.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly IStripeService _stripe;
    private readonly IConfiguration _config;

    public BillingController(IStripeService stripe, IConfiguration config)
    {
        _stripe = stripe;
        _config = config;
    }

    /// <summary>Creates a Stripe Checkout session and returns the redirect URL.</summary>
    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("AgentScope_UserId")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Missing or invalid AgentScope_UserId claim." });

        var priceId = request.PriceId ?? _config["Stripe:ProPriceId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(priceId))
            return BadRequest(new { error = "priceId is required." });

        var result = await _stripe.CreateCheckoutSessionAsync(userId, priceId, ct);
        return result.Match<IActionResult>(
            success => Ok(new { checkoutUrl = success }),
            failure => BadRequest(new { error = failure.Message, code = failure.Code })
        );
    }

    /// <summary>Receives Stripe webhook events. Must read raw body.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        string json;
        using (var reader = new StreamReader(Request.Body))
            json = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        var result = await _stripe.HandleWebhookEventAsync(json, signature, ct);
        return result.Match<IActionResult>(
            _ => Ok(),
            failure => BadRequest(new { error = failure.Message, code = failure.Code })
        );
    }
}

public record CreateCheckoutRequest(string? PriceId);
