using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentScope.Api;

public sealed class RetentionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public RetentionCleanupService(IServiceScopeFactory scopeFactory, ILogger<RetentionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger first run by 30s so the app can finish starting up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentScopeDbContext>();

            var users = await db.Users.AsNoTracking().ToListAsync(ct);

            var totalTraces  = 0;
            var totalMetrics = 0;

            foreach (var user in users)
            {
                var retentionDays = user.Tier switch
                {
                    SubscriptionTier.Free       => 7,
                    SubscriptionTier.Pro        => 30,
                    SubscriptionTier.Business   => 90,
                    SubscriptionTier.Enterprise => 90,
                    _                           => 7,
                };

                var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

                // Get app IDs for this user
                var appIds = await db.Applications
                    .Where(a => a.OwnerId == user.Id)
                    .Select(a => a.Id)
                    .ToListAsync(ct);

                if (appIds.Count == 0) continue;

                // Delete old traces (cascades to spans via FK)
                var deletedTraces = await db.Traces
                    .Where(t => appIds.Contains(t.ApplicationId) && t.StartedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                // Delete old metric points
                var deletedMetrics = await db.MetricPoints
                    .Where(m => appIds.Contains(m.ApplicationId) && m.WindowStart < cutoff)
                    .ExecuteDeleteAsync(ct);

                totalTraces  += deletedTraces;
                totalMetrics += deletedMetrics;
            }

            if (totalTraces > 0 || totalMetrics > 0)
            {
                _logger.LogInformation(
                    "Retention cleanup: deleted {Traces} traces, {Metrics} metric points",
                    totalTraces, totalMetrics);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retention cleanup failed");
        }
    }
}
