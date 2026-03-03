using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AgentScope.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for AgentScope.
/// All entity configurations are in the Configurations/ folder to keep
/// this file lean — just one <see cref="OnModelCreating"/> call per aggregate.
/// </summary>
public sealed class AgentScopeDbContext : DbContext
{
    public AgentScopeDbContext(DbContextOptions<AgentScopeDbContext> options) : base(options) { }

    public DbSet<User>         Users         => Set<User>();
    public DbSet<Application>  Applications  => Set<Application>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Trace>        Traces        => Set<Trace>();
    public DbSet<Span>         Spans         => Set<Span>();
    public DbSet<AgentEvent>   AgentEvents   => Set<AgentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new TraceConfiguration());
        modelBuilder.ApplyConfiguration(new SpanConfiguration());
        modelBuilder.ApplyConfiguration(new AgentEventConfiguration());
    }
}
