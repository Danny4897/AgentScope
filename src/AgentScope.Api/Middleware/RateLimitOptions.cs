namespace AgentScope.Api.Middleware;

/// <summary>
/// Configuration for the Redis sliding-window rate limiter.
/// Binds to the "RateLimit" section in appsettings.json.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Max requests per window per client IP.</summary>
    public int IpPermitLimit { get; set; } = 60;

    /// <summary>Max requests per window per API key (header x-api-key).</summary>
    public int ApiKeyPermitLimit { get; set; } = 300;

    /// <summary>Sliding window duration in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
