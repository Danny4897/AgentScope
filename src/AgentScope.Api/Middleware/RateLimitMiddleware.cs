using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentScope.Api.Middleware;

/// <summary>
/// Redis-backed sliding window rate limiter.
/// Dual bucket: per-IP (rl:ip:{ip}) + per-API-key (rl:key:{hash}).
/// Fail-open on Redis errors.
/// </summary>
public sealed class RateLimitMiddleware
{
    private const string ApiKeyHeader = "x-api-key";

    /// <summary>
    /// Atomic Lua script: remove expired entries, add current request, return count.
    /// Uses Redis server time to avoid clock-drift issues.
    /// </summary>
    private static readonly LuaScript SlidingWindowScript = LuaScript.Prepare(
        """
        local key     = @key
        local window  = tonumber(@window)
        local member  = @member
        local now     = redis.call('TIME')
        local now_us  = tonumber(now[1]) * 1000000 + tonumber(now[2])
        local min_score = now_us - window * 1000000
        redis.call('ZREMRANGEBYSCORE', key, '-inf', min_score)
        redis.call('ZADD', key, now_us, member)
        redis.call('EXPIRE', key, window + 1)
        return redis.call('ZCARD', key)
        """);

    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<RateLimitOptions> options,
        IConnectionMultiplexer? redis = null)
    {
        // If Redis is unavailable, fail-open
        if (redis is null || !redis.IsConnected)
        {
            await _next(context);
            return;
        }

        var opts      = options.Value;
        var db        = redis.GetDatabase();
        var clientIp  = GetClientIp(context);
        var member    = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid():N}";

        // Per-IP check
        var ipResult = await EvalAsync(db, $"rl:ip:{clientIp}", opts.WindowSeconds, member);
        if (!ipResult.IsSuccess)
        {
            _logger.LogWarning("Rate limit Redis error (IP): {Error}", ipResult.Error);
            await _next(context);
            return;
        }

        var ipRemaining = Math.Max(0, opts.IpPermitLimit - (int)ipResult.Value);
        context.Response.Headers["X-RateLimit-Limit-Ip"]     = opts.IpPermitLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining-Ip"] = ipRemaining.ToString();

        if (ipResult.Value > opts.IpPermitLimit)
        {
            await Write429(context, opts.WindowSeconds);
            return;
        }

        // Per-API-key check (only if header present)
        if (context.Request.Headers.TryGetValue(ApiKeyHeader, out var rawKey)
            && !string.IsNullOrWhiteSpace(rawKey))
        {
            var apiKey   = rawKey.ToString();
            var keyId    = $"rl:key:{apiKey[..Math.Min(8, apiKey.Length)]}:{Hash(apiKey)}";
            var keyMember = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid():N}";

            var keyResult = await EvalAsync(db, keyId, opts.WindowSeconds, keyMember);
            if (!keyResult.IsSuccess)
            {
                _logger.LogWarning("Rate limit Redis error (key): {Error}", keyResult.Error);
                await _next(context);
                return;
            }

            var keyRemaining = Math.Max(0, opts.ApiKeyPermitLimit - (int)keyResult.Value);
            context.Response.Headers["X-RateLimit-Limit-Key"]     = opts.ApiKeyPermitLimit.ToString();
            context.Response.Headers["X-RateLimit-Remaining-Key"] = keyRemaining.ToString();

            if (keyResult.Value > opts.ApiKeyPermitLimit)
            {
                await Write429(context, opts.WindowSeconds);
                return;
            }
        }

        await _next(context);
    }

    private static async Task<RateLimitResult> EvalAsync(IDatabase db, string key, int windowSeconds, string member)
    {
        try
        {
            var result = await db.ScriptEvaluateAsync(
                SlidingWindowScript,
                new { key = (RedisKey)key, window = windowSeconds, member });
            return RateLimitResult.Ok((long)result);
        }
        catch (Exception ex)
        {
            return RateLimitResult.Fail(ex.Message);
        }
    }

    private static async Task Write429(HttpContext ctx, int windowSeconds)
    {
        ctx.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
        ctx.Response.ContentType = "application/json";
        ctx.Response.Headers["Retry-After"] = windowSeconds.ToString();
        await ctx.Response.WriteAsJsonAsync(new
        {
            error       = "rate_limit_exceeded",
            retry_after = windowSeconds
        });
    }

    private static string GetClientIp(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd)
            && !string.IsNullOrWhiteSpace(fwd))
            return fwd.ToString().Split(',', StringSplitOptions.TrimEntries)[0];

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>SHA-256 prefix so full API key never appears in Redis key names.</summary>
    private static string Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private readonly record struct RateLimitResult(bool IsSuccess, long Value, string? Error)
    {
        public static RateLimitResult Ok(long count)    => new(true,  count, null);
        public static RateLimitResult Fail(string err)  => new(false, 0,     err);
    }
}

public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitMiddleware>();
}
