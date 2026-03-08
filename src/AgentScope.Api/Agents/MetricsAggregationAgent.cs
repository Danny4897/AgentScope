using System.Text.Json;
using AgentScope.Api.Models.Otlp;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using MonadicSharp;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Persistence.Core;

namespace AgentScope.Api.Agents;

public sealed record MetricsIngestionInput(
    OtlpMetricsRequest Payload,
    Application Application,
    User Owner);

public sealed record MetricsIngestionOutput(int MetricPointsIngested);

/// <summary>
/// Parses OTLP metrics, aggregates them into 1-min / 5-min / 1-h time windows,
/// and persists the resulting <see cref="MetricPoint"/> rows.
/// </summary>
public sealed class MetricsAggregationAgent : IAgent<MetricsIngestionInput, MetricsIngestionOutput>
{
    private readonly IMetricRepository _metricRepo;
    private readonly IUnitOfWork _uow;

    public MetricsAggregationAgent(IMetricRepository metricRepo, IUnitOfWork uow)
    {
        _metricRepo = metricRepo;
        _uow        = uow;
    }

    public string Name => "MetricsAggregationAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.AccessDatabase;

    public async Task<Result<MetricsIngestionOutput>> ExecuteAsync(
        MetricsIngestionInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // ── Quota check ──────────────────────────────────────────────────────
        var quota = GetMonthlyMetricQuota(input.Owner.Tier);
        if (quota != long.MaxValue)
        {
            var periodStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var countResult = await _metricRepo.CountByApplicationAsync(
                input.Application.Id, periodStart, DateTimeOffset.UtcNow, cancellationToken);

            if (!countResult.IsSuccess)
                return Result<MetricsIngestionOutput>.Failure(countResult.Error);

            var incoming = CountIncomingDataPoints(input.Payload);
            if (countResult.Value + incoming > quota)
                return Result<MetricsIngestionOutput>.Failure(
                    DomainErrors.Subscriptions.EventQuotaExceeded(quota));
        }

        // ── Parse and aggregate ───────────────────────────────────────────────
        var points = new List<MetricPoint>();
        var windows = new[] { ("1min", TimeSpan.FromMinutes(1)), ("5min", TimeSpan.FromMinutes(5)), ("1h", TimeSpan.FromHours(1)) };

        foreach (var rm in input.Payload.ResourceMetrics)
        foreach (var sm in rm.ScopeMetrics)
        foreach (var metric in sm.Metrics)
        {
            var (kind, dataPoints) = ExtractDataPoints(metric);
            if (dataPoints.Count == 0) continue;

            var attrsJson = JsonSerializer.Serialize(
                dataPoints[0].Attributes.ToDictionary(kv => kv.Key, kv => kv.Value?.ToDisplayString() ?? ""));

            // Raw points (one per data point)
            foreach (var dp in dataPoints)
            {
                var ts = UnixNanoToDateTimeOffset(dp.TimeUnixNano);
                points.Add(MetricPoint.Create(
                    input.Application.Id, metric.Name, metric.Unit, kind,
                    "raw", ts, dp.GetValue(), dp.GetValue(), dp.GetValue(), 1, attrsJson));
            }

            // Aggregated windows
            foreach (var (windowName, windowSize) in windows)
            {
                var grouped = dataPoints
                    .GroupBy(dp => FloorTo(UnixNanoToDateTimeOffset(dp.TimeUnixNano), windowSize));

                foreach (var group in grouped)
                {
                    var vals = group.Select(dp => dp.GetValue()).ToList();
                    points.Add(MetricPoint.Create(
                        input.Application.Id, metric.Name, metric.Unit, kind,
                        windowName, group.Key,
                        vals.Min(), vals.Max(), vals.Sum(), vals.Count, attrsJson));
                }
            }
        }

        if (points.Count > 0)
        {
            var addResult = await _metricRepo.AddRangeAsync(points, cancellationToken);
            if (!addResult.IsSuccess)
                return Result<MetricsIngestionOutput>.Failure(addResult.Error);

            var saveResult = await _uow.SaveChangesAsync(cancellationToken);
            if (!saveResult.IsSuccess)
                return Result<MetricsIngestionOutput>.Failure(saveResult.Error);
        }

        return Result<MetricsIngestionOutput>.Success(new MetricsIngestionOutput(points.Count));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MetricKind kind, List<OtlpNumberDataPoint> points) ExtractDataPoints(OtlpMetric metric)
    {
        if (metric.Gauge is not null)
            return (MetricKind.Gauge, metric.Gauge.DataPoints);
        if (metric.Sum is not null)
            return (MetricKind.Sum, metric.Sum.DataPoints);
        if (metric.Histogram is not null)
        {
            // Convert histogram to synthetic number points using sum/count
            var pts = metric.Histogram.DataPoints.Select(h => new OtlpNumberDataPoint
            {
                Attributes       = h.Attributes,
                TimeUnixNano     = h.TimeUnixNano,
                StartTimeUnixNano = h.StartTimeUnixNano,
                AsDouble         = h.Count > 0 ? h.Sum / h.Count : 0,
            }).ToList();
            return (MetricKind.Histogram, pts);
        }
        return (MetricKind.Gauge, []);
    }

    private static long CountIncomingDataPoints(OtlpMetricsRequest req) =>
        req.ResourceMetrics
            .SelectMany(rm => rm.ScopeMetrics)
            .SelectMany(sm => sm.Metrics)
            .Sum(m =>
                (m.Gauge?.DataPoints.Count ?? 0) +
                (m.Sum?.DataPoints.Count ?? 0) +
                (m.Histogram?.DataPoints.Count ?? 0));

    private static DateTimeOffset UnixNanoToDateTimeOffset(string unixNano)
    {
        if (!long.TryParse(unixNano, out var nanos)) return DateTimeOffset.UtcNow;
        return DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000);
    }

    private static DateTimeOffset FloorTo(DateTimeOffset dt, TimeSpan window)
    {
        var ticks = dt.UtcTicks / window.Ticks * window.Ticks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static long GetMonthlyMetricQuota(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free       => 10_000L,
        SubscriptionTier.Pro        => 500_000L,
        SubscriptionTier.Business   => long.MaxValue,
        SubscriptionTier.Enterprise => long.MaxValue,
        _                           => 10_000L,
    };
}
