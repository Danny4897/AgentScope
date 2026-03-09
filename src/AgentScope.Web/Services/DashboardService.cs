using System.Net.Http.Json;
using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentScope.Web.Services;

// ── Railway Visualizer DTOs ──────────────────────────────────────────────────

public sealed record RailwayNode(
    string     SpanId,
    string?    ParentSpanId,
    string     Name,
    string?    Operation,
    bool?      IsSuccess,
    string?    ErrorCode,
    string?    StatusMessage,
    string?    ErrorKind,        // "Monadic" | "Exception" | null
    long       DurationMs,
    SpanStatus Status,
    List<RailwayNode> Children);

// ── Cost & Token DTOs ────────────────────────────────────────────────────────

public sealed record CostDashboard(
    decimal TotalCostUsd,
    long    TotalTokens,
    decimal TodayCostUsd,
    long    TodayTokens,
    List<ModelCostRow> ByModel);

public sealed record ModelCostRow(
    string  Model,
    long    Tokens,
    decimal CostUsd,
    double  Percentage);

public sealed record ErrorSummaryRow(
    string ErrorType,
    string Kind,       // "Monadic" | "Exception"
    int Count,
    double Percentage,
    string Suggestion);

public sealed record DashboardSummary(
    int TotalTraces,
    int FailedTraces,
    double AvgLatencyMs,
    int ActiveApps)
{
    public double SuccessRate => TotalTraces > 0
        ? (TotalTraces - FailedTraces) * 100.0 / TotalTraces
        : 100.0;
}

// ── Prompt CMS DTOs ──────────────────────────────────────────────────────────

public sealed record PromptSummary(
    Guid   Id,
    Guid   ApplicationId,
    string Slug,
    int    Version,
    string Content,
    string? ChangeNote,
    bool   IsActive,
    DateTimeOffset PublishedAt);

public sealed class DashboardService
{
    private readonly IDbContextFactory<AgentScopeDbContext> _factory;
    private readonly IHttpClientFactory _httpFactory;

    public DashboardService(IDbContextFactory<AgentScopeDbContext> factory, IHttpClientFactory httpFactory)
    {
        _factory     = factory;
        _httpFactory = httpFactory;
    }

    public async Task<DashboardSummary> GetSummaryAsync(Guid? userId = null, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        // Resolve app IDs for tenant isolation
        var appIds = await GetUserAppIdsAsync(db, userId, ct);

        var tracesQuery = db.Traces.Where(t => t.StartedAt >= since);
        if (appIds != null) tracesQuery = tracesQuery.Where(t => appIds.Contains(t.ApplicationId));

        var totalTraces  = await tracesQuery.CountAsync(ct);
        var failedTraces = await tracesQuery.Where(t => t.Status == TraceStatus.Failure).CountAsync(ct);

        var endedTraces = await tracesQuery
            .Where(t => t.EndedAt != null)
            .Select(t => new { t.StartedAt, EndedAt = t.EndedAt!.Value })
            .ToListAsync(ct);

        var avgMs = endedTraces.Count > 0
            ? endedTraces.Average(t => (t.EndedAt - t.StartedAt).TotalMilliseconds)
            : 0;

        var appsQuery = db.Applications.Where(a => a.IsActive);
        if (userId.HasValue) appsQuery = appsQuery.Where(a => a.OwnerId == userId.Value);
        var activeApps = await appsQuery.CountAsync(ct);

        return new DashboardSummary(totalTraces, failedTraces, avgMs, activeApps);
    }

    public async Task<List<Trace>> GetRecentTracesAsync(Guid? userId = null, int limit = 20, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var appIds = await GetUserAppIdsAsync(db, userId, ct);

        var query = db.Traces.AsQueryable();
        if (appIds != null) query = query.Where(t => appIds.Contains(t.ApplicationId));

        return await query.OrderByDescending(t => t.StartedAt).Take(limit).ToListAsync(ct);
    }

    public async Task<List<Application>> GetApplicationsAsync(Guid? userId = null, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Applications.Where(a => a.IsActive);
        if (userId.HasValue) query = query.Where(a => a.OwnerId == userId.Value);
        return await query.OrderBy(a => a.Name).ToListAsync(ct);
    }

    public async Task<(List<Trace> Items, int Total)> GetTracesPageAsync(
        Guid? userId,
        Guid? applicationId,
        TraceStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var appIds = await GetUserAppIdsAsync(db, userId, ct);

        var query = db.Traces.AsQueryable();
        if (appIds != null)        query = query.Where(t => appIds.Contains(t.ApplicationId));
        if (applicationId.HasValue) query = query.Where(t => t.ApplicationId == applicationId.Value);
        if (status.HasValue)        query = query.Where(t => t.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(t => t.Spans)
            .OrderByDescending(t => t.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Trace?> GetTraceDetailAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Traces
            .Include(t => t.Spans)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<List<MetricPoint>> GetMetricsAsync(
        Guid? userId = null,
        Guid? applicationId = null,
        string timeWindow = "1min",
        CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var appIds = await GetUserAppIdsAsync(db, userId, ct);

        var query = db.MetricPoints
            .Where(m => m.TimeWindow == timeWindow && m.WindowStart >= DateTimeOffset.UtcNow.AddHours(-6));
        if (appIds != null)        query = query.Where(m => appIds.Contains(m.ApplicationId));
        if (applicationId.HasValue) query = query.Where(m => m.ApplicationId == applicationId.Value);

        return await query.OrderByDescending(m => m.WindowStart).ToListAsync(ct);
    }

    // ── Railway Visualizer ───────────────────────────────────────────────────

    public async Task<List<RailwayNode>> GetRailwayGraphAsync(Guid traceId, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();

        var spans = await db.Spans
            .Where(s => s.TraceId == traceId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct);

        // Build node map
        var nodeMap = spans.ToDictionary(
            s => s.SpanId,
            s => new RailwayNode(
                SpanId:        s.SpanId,
                ParentSpanId:  s.ParentSpanId,
                Name:          s.Name,
                Operation:     s.RailwayOp?.ToString(),
                IsSuccess:     s.ResultIsSuccess,
                ErrorCode:     s.RailwayErrorCode,
                StatusMessage: s.StatusMessage,
                ErrorKind:     s.ResultIsSuccess == false && s.StatusMessage is not null
                                   ? Classify(s.StatusMessage).Kind
                                   : null,
                DurationMs:    s.DurationMs,
                Status:        s.Status,
                Children:      []));

        var roots = new List<RailwayNode>();
        foreach (var node in nodeMap.Values)
        {
            if (node.ParentSpanId != null && nodeMap.TryGetValue(node.ParentSpanId, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    // ── Cost & Token Usage ───────────────────────────────────────────────────

    public async Task<CostDashboard> GetCostDashboardAsync(Guid? userId = null, CancellationToken ct = default)
    {
        await using var db   = _factory.CreateDbContext();
        var appIds           = await GetUserAppIdsAsync(db, userId, ct);
        var from             = DateTimeOffset.UtcNow.AddDays(-30);

        var query = db.TokenUsages.Where(t => t.RecordedAt >= from);
        if (appIds != null) query = query.Where(t => appIds.Contains(t.ApplicationId));

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0)
            return new CostDashboard(0m, 0L, 0m, 0L, []);

        var todayStart = DateTimeOffset.UtcNow.Date;
        var todayRows  = rows.Where(r => r.RecordedAt >= todayStart).ToList();

        var total = rows.Sum(r => r.CostUsd);
        var byModel = rows
            .GroupBy(r => r.Model, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ModelCostRow(
                Model:      g.Key,
                Tokens:     g.Sum(r => (long)r.TotalTokens),
                CostUsd:    g.Sum(r => r.CostUsd),
                Percentage: total > 0m ? (double)(g.Sum(r => r.CostUsd) / total * 100) : 0))
            .OrderByDescending(r => r.CostUsd)
            .ToList();

        return new CostDashboard(
            total,
            rows.Sum(r => (long)r.TotalTokens),
            todayRows.Sum(r => r.CostUsd),
            todayRows.Sum(r => (long)r.TotalTokens),
            byModel);
    }

    public async Task<List<ErrorSummaryRow>> GetErrorSummaryAsync(Guid? userId = null, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var appIds = await GetUserAppIdsAsync(db, userId, ct);

        // Resolve trace IDs for tenant isolation
        List<Guid>? traceIds = null;
        if (appIds != null)
            traceIds = await db.Traces
                .Where(t => appIds.Contains(t.ApplicationId))
                .Select(t => t.Id)
                .ToListAsync(ct);

        var query = db.Spans.Where(s => s.Status == SpanStatus.Error && s.StatusMessage != null);
        if (traceIds != null) query = query.Where(s => traceIds.Contains(s.TraceId));

        var messages = await query.Select(s => s.StatusMessage!).ToListAsync(ct);

        if (messages.Count == 0) return MockErrorSummary();

        var grouped = messages
            .GroupBy(Classify)
            .Select(g => new { g.Key.Type, g.Key.Kind, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        var total = grouped.Sum(x => x.Count);
        return grouped.Select(g => new ErrorSummaryRow(
            g.Type, g.Kind, g.Count,
            total > 0 ? g.Count * 100.0 / total : 0,
            Suggestion(g.Type)
        )).ToList();
    }

    private static (string Type, string Kind) Classify(string msg) => msg switch
    {
        var m when m.StartsWith("Result.Failure", StringComparison.Ordinal) => (Trim(m), "Monadic"),
        var m when m.StartsWith("Option.None",    StringComparison.Ordinal) => (Trim(m), "Monadic"),
        var m when m.StartsWith("DomainError",    StringComparison.Ordinal) => (Trim(m), "Monadic"),
        var m                                                                => (Trim(m), "Exception"),
    };

    private static string Trim(string m) => m.Length > 48 ? m[..48] + "…" : m;

    private static string Suggestion(string t) => t switch
    {
        var s when s.Contains("upstream timeout")  => "Add retry with exponential backoff on LLM calls (Polly); set SLA budget alerts.",
        var s when s.Contains("tool not found")    => "Validate tool registry at startup; add a NoopTool fallback for unknown tools.",
        var s when s.Contains("context missing")   => "Guard pipeline entry with explicit null-checks; propagate context via Result<T>.",
        var s when s.Contains("rate limit")        => "Implement token-bucket per tenant; surface usage warnings before hard cutoff.",
        var s when s.Contains("HttpRequest")       => "Wrap external HTTP calls in Result.Try(); add Polly circuit breaker + timeout.",
        var s when s.Contains("JsonException")     => "Validate LLM response schema before deserialization; sanitize malformed outputs.",
        var s when s.Contains("CONTEXT_OVERFLOW")  => "Use sliding-window context pruning; summarize conversation history before limit.",
        _                                          => "Add structured error context (span attributes) to speed up root-cause analysis.",
    };

    private static List<ErrorSummaryRow> MockErrorSummary() =>
    [
        new("Result.Failure: upstream timeout",         "Monadic",   34, 34.0, "Add retry with exponential backoff on LLM calls (Polly); set SLA budget alerts."),
        new("Result.Failure: tool not found",           "Monadic",   22, 22.0, "Validate tool registry at startup; add a NoopTool fallback for unknown tools."),
        new("HttpRequestException: 503 Unavailable",   "Exception", 18, 18.0, "Wrap external HTTP calls in Result.Try(); add Polly circuit breaker + timeout."),
        new("Option.None: context missing",             "Monadic",   12, 12.0, "Guard pipeline entry with explicit null-checks; propagate context via Result<T>."),
        new("CONTEXT_OVERFLOW: token limit exceeded",  "Exception",  8,  8.0, "Use sliding-window context pruning; summarize conversation history before limit."),
        new("JsonException: unexpected token",         "Exception",  4,  4.0, "Validate LLM response schema before deserialization; sanitize malformed outputs."),
        new("DomainError: rate limit exceeded",        "Monadic",    2,  2.0, "Implement token-bucket per tenant; surface usage warnings before hard cutoff."),
    ];

    // ── Prompt CMS ───────────────────────────────────────────────────────────

    public async Task<List<PromptSummary>> GetPromptsAsync(Guid applicationId, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.IsActive)
            .OrderBy(p => p.Slug)
            .ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<List<PromptSummary>> GetPromptHistoryAsync(Guid applicationId, string slug, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug)
            .OrderByDescending(p => p.Version)
            .ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<bool> CreatePromptAsync(Guid applicationId, string slug, string content, string? changeNote, CancellationToken ct = default)
    {
        var http = GetApiClient();
        var resp = await http.PostAsJsonAsync(
            $"api/v1/prompts/{applicationId}",
            new { slug, content, changeNote },
            ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> PublishPromptVersionAsync(Guid applicationId, string slug, string content, string? changeNote, CancellationToken ct = default)
    {
        var http = GetApiClient();
        var resp = await http.PutAsJsonAsync(
            $"api/v1/prompts/{applicationId}/{Uri.EscapeDataString(slug)}",
            new { content, changeNote },
            ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RollbackPromptAsync(Guid applicationId, string slug, int version, CancellationToken ct = default)
    {
        var http = GetApiClient();
        var resp = await http.PostAsJsonAsync(
            $"api/v1/prompts/{applicationId}/{Uri.EscapeDataString(slug)}/rollback",
            new { version },
            ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePromptAsync(Guid applicationId, string slug, CancellationToken ct = default)
    {
        var http = GetApiClient();
        var resp = await http.DeleteAsync(
            $"api/v1/prompts/{applicationId}/{Uri.EscapeDataString(slug)}",
            ct);
        return resp.IsSuccessStatusCode;
    }

    private HttpClient GetApiClient() => _httpFactory.CreateClient("AgentScopeApi");

    private static PromptSummary ToSummary(Prompt p) =>
        new(p.Id, p.ApplicationId, p.Slug, p.Version, p.Content, p.ChangeNote, p.IsActive, p.PublishedAt);

    private static async Task<List<Guid>?> GetUserAppIdsAsync(AgentScopeDbContext db, Guid? userId, CancellationToken ct)
    {
        if (!userId.HasValue) return null;
        return await db.Applications
            .Where(a => a.OwnerId == userId.Value)
            .Select(a => a.Id)
            .ToListAsync(ct);
    }
}
