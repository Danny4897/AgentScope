using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;

namespace AgentScope.Api.Middleware;

/// <summary>
/// Validates the <c>x-api-key</c> header on OTLP ingestion endpoints,
/// resolves the <see cref="Application"/> and its owner <see cref="User"/>,
/// and attaches them to <see cref="HttpContext.Items"/> for downstream use.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "x-api-key";

    /// <summary>Key used to store the resolved Application in HttpContext.Items.</summary>
    public const string ApplicationKey = "AgentScope.Application";

    /// <summary>Key used to store the resolved owner User in HttpContext.Items.</summary>
    public const string OwnerKey = "AgentScope.Owner";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApplicationRepository appRepo,
        IUserRepository userRepo)
    {
        // Only apply to /v1/ ingestion endpoints
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var rawKey)
            || string.IsNullOrWhiteSpace(rawKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing x-api-key header." });
            return;
        }

        var appResult = await appRepo.GetByApiKeyAsync(rawKey.ToString(), context.RequestAborted);
        if (!appResult.IsSuccess)
        {
            _logger.LogWarning("Invalid API key attempted: {KeyPrefix}***", rawKey.ToString()[..Math.Min(4, rawKey.ToString().Length)]);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive API key." });
            return;
        }

        var userResult = await userRepo.GetByIdAsync(appResult.Value.OwnerId, context.RequestAborted);
        if (!userResult.IsSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Application owner not found." });
            return;
        }

        context.Items[ApplicationKey] = appResult.Value;
        context.Items[OwnerKey]       = userResult.Value;

        await _next(context);
    }
}

/// <summary>Extension to register the middleware cleanly in Program.cs.</summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyMiddleware>();
}
