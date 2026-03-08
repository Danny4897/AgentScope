namespace AgentScope.Domain.Entities;

/// <summary>
/// An aggregated OTLP metric data point received via POST /v1/metrics.
/// Values are aggregated per time window (1min, 5min, 1h) by the MetricsAggregationAgent.
/// </summary>
public sealed class MetricPoint
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string MetricName { get; private set; }
    public string Unit { get; private set; }
    public MetricKind Kind { get; private set; }
    public string TimeWindow { get; private set; }      // "raw" | "1min" | "5min" | "1h"
    public DateTimeOffset WindowStart { get; private set; }
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Sum { get; private set; }
    public long Count { get; private set; }
    public double Avg => Count > 0 ? Sum / Count : 0;
    public string AttributesJson { get; private set; }

    private MetricPoint() { MetricName = null!; Unit = null!; TimeWindow = null!; AttributesJson = null!; }

    public static MetricPoint Create(
        Guid applicationId,
        string metricName,
        string unit,
        MetricKind kind,
        string timeWindow,
        DateTimeOffset windowStart,
        double min,
        double max,
        double sum,
        long count,
        string attributesJson = "{}") =>
        new()
        {
            Id            = Guid.NewGuid(),
            ApplicationId = applicationId,
            MetricName    = metricName,
            Unit          = unit,
            Kind          = kind,
            TimeWindow    = timeWindow,
            WindowStart   = windowStart,
            Min           = min,
            Max           = max,
            Sum           = sum,
            Count         = count,
            AttributesJson = attributesJson,
        };
}

public enum MetricKind { Gauge, Sum, Histogram }
