using AgentScope.Domain.Ports;
using AgentScope.Domain.Services;
using AgentScope.Infrastructure.Persistence;
using AgentScope.Infrastructure.Persistence.Repositories;
using AgentScope.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MonadicSharp.Persistence.Core;
using MonadicSharp.Persistence.Implementations;
using StackExchange.Redis;

namespace AgentScope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Support Railway's DATABASE_URL (postgresql://user:pass@host:port/db) as fallback
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres or DATABASE_URL is required.");

        services.AddDbContext<AgentScopeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AgentScopeDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IUserRepository,         UserRepository>();
        services.AddScoped<IApplicationRepository,  ApplicationRepository>();
        services.AddScoped<ITraceRepository,        TraceRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IMetricRepository,       MetricRepository>();
        services.AddScoped<ITokenUsageRepository,   TokenUsageRepository>();
        services.AddScoped<IPromptRepository,       PromptRepository>();
        services.AddScoped<IHitlReviewRepository,   HitlReviewRepository>();
        services.AddSingleton<ILlmPricingService,   LlmPricingService>();
        services.AddScoped<IStripeService,          StripeService>();

        // Distributed cache — Redis when REDIS_URL is set, otherwise in-memory fallback
        var redisUrl = configuration.GetConnectionString("Redis")
                    ?? Environment.GetEnvironmentVariable("REDIS_URL");

        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddStackExchangeRedisCache(opts => opts.Configuration = redisUrl);
            // Direct multiplexer for rate limiter (sorted-set sliding window)
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisUrl));
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // Unit of Work wrapping the same DbContext scope
        services.AddScoped<IUnitOfWork>(sp =>
            new EfCoreUnitOfWork(
                sp.GetRequiredService<AgentScopeDbContext>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EfCoreUnitOfWork>>()));

        return services;
    }
}
