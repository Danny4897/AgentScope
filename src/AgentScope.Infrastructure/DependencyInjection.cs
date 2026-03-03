using AgentScope.Domain.Ports;
using AgentScope.Infrastructure.Persistence;
using AgentScope.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentScope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

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

        return services;
    }
}
