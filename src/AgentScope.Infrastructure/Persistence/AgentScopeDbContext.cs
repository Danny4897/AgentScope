using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgentScope.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for AgentScope.
/// All entity configurations are in the Configurations/ folder to keep
/// this file lean — just one <see cref="OnModelCreating"/> call per aggregate.
/// </summary>
public sealed class AgentScopeDbContext : IdentityDbContext<IdentityUser>
{
    public AgentScopeDbContext(DbContextOptions<AgentScopeDbContext> options) : base(options) { }

    public new DbSet<User>     Users         => Set<User>();
    public DbSet<Application>  Applications  => Set<Application>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Trace>        Traces        => Set<Trace>();
    public DbSet<Span>         Spans         => Set<Span>();
    public DbSet<AgentEvent>   AgentEvents   => Set<AgentEvent>();
    public DbSet<MetricPoint>  MetricPoints  => Set<MetricPoint>();
    public DbSet<TokenUsage>   TokenUsages   => Set<TokenUsage>();
    public DbSet<Prompt>       Prompts        => Set<Prompt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new ApplicationConfiguration());
        builder.ApplyConfiguration(new SubscriptionConfiguration());
        builder.ApplyConfiguration(new TraceConfiguration());
        builder.ApplyConfiguration(new SpanConfiguration());
        builder.ApplyConfiguration(new AgentEventConfiguration());
        builder.ApplyConfiguration(new MetricPointConfiguration());
        builder.ApplyConfiguration(new TokenUsageConfiguration());
        builder.ApplyConfiguration(new PromptConfiguration());
    }
}
