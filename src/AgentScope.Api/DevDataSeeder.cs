using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentScope.Api;

/// <summary>
/// Popola il DB con dati realistici in Development.
/// Idempotente: non fa nulla se i dati esistono già.
/// </summary>
public static class DevDataSeeder
{
    private static readonly Random Rng = new(42);

    private static readonly string[] ErrorMessages =
    [
        "Result.Failure: upstream timeout",
        "Result.Failure: upstream timeout",
        "Result.Failure: upstream timeout",
        "Result.Failure: tool not found",
        "Result.Failure: tool not found",
        "Option.None: context missing",
        "Option.None: context missing",
        "HttpRequestException: 503 Service Unavailable",
        "HttpRequestException: 503 Service Unavailable",
        "HttpRequestException: 503 Service Unavailable",
        "CONTEXT_OVERFLOW: token limit exceeded",
        "JsonException: unexpected token in response",
        "DomainError: rate limit exceeded",
    ];

    public static async Task SeedAsync(AgentScopeDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        // ── User demo ─────────────────────────────────────────────────────────
        var user = User.Create("demo@agentscope.io", "Demo User");
        user.UpgradeTier(SubscriptionTier.Pro);
        db.Users.Add(user);

        // ── Applications ──────────────────────────────────────────────────────
        var appOrchestrator = Application.Create(user.Id, "Orchestrator API",    "sk_live_demo_orchestrator_001", "Main LLM orchestration service");
        var appDocBot       = Application.Create(user.Id, "DocBot",              "sk_live_demo_docbot_002",       "Document Q&A agent");
        db.Applications.AddRange(appOrchestrator, appDocBot);

        // ── Traces + Spans ────────────────────────────────────────────────────
        var pipelines = new[]
        {
            ("InvoiceExtractionPipeline", 3, 8),
            ("CustomerQueryAgent",        2, 5),
            ("SummaryGenerationChain",    2, 6),
            ("ClassificationPipeline",    1, 3),
            ("RAGRetrievalAgent",         3, 7),
            ("ContractAnalysisPipeline",  4, 10),
            ("EmailDraftingAgent",        2, 4),
            ("DataValidationChain",       1, 3),
        };

        var statuses = new[]
        {
            TraceStatus.Success, TraceStatus.Success, TraceStatus.Success, TraceStatus.Success,
            TraceStatus.Success, TraceStatus.Success, TraceStatus.Success,
            TraceStatus.Failure, TraceStatus.Failure,
            TraceStatus.Timeout,
        };

        var now = DateTimeOffset.UtcNow;

        foreach (var app in new[] { appOrchestrator, appDocBot })
        {
            for (int i = 0; i < 12; i++)
            {
                var (pipeline, minSpans, maxSpans) = pipelines[Rng.Next(pipelines.Length)];
                var status     = statuses[Rng.Next(statuses.Length)];
                var spanCount  = Rng.Next(minSpans, maxSpans + 1);
                var startedAt  = now.AddHours(-Rng.Next(0, 22)).AddMinutes(-Rng.Next(0, 59));
                var durationMs = Rng.Next(120, 4500);

                var traceId = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..16].ToLowerInvariant();
                var trace   = Trace.Create(app.Id, traceId, pipeline, startedAt);
                trace.Complete(status, startedAt.AddMilliseconds(durationMs));
                db.Traces.Add(trace);

                // Spans — simulate a realistic pipeline waterfall
                var spanStart  = startedAt;
                var rootSpanId = SpanHex();

                // Models used for LLM spans
                var models = new[] { "gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet" };

                for (int s = 0; s < spanCount; s++)
                {
                    var spanDuration = durationMs / spanCount + Rng.Next(-30, 80);
                    spanDuration = Math.Max(10, spanDuration);
                    var spanEnd  = spanStart.AddMilliseconds(spanDuration);

                    var isError    = (status == TraceStatus.Failure && s == spanCount - 1)
                                  || (status == TraceStatus.Timeout  && s == spanCount - 1);
                    var spanStatus = isError ? SpanStatus.Error : SpanStatus.Ok;
                    var spanKind   = s == 0 ? SpanKind.Server
                                   : Rng.Next(2) == 0 ? SpanKind.Client : SpanKind.Internal;

                    var spanName = s switch
                    {
                        0 => pipeline,
                        1 => "llm.completion",
                        2 => "retriever.search",
                        3 => "tool.execute",
                        _ => $"step.{s}",
                    };

                    // Standard gen_ai.* attributes for LLM spans
                    var model      = models[Rng.Next(models.Length)];
                    var promptTok  = Rng.Next(200, 2000);
                    var outputTok  = Rng.Next(50, 500);
                    var attrs      = s == 1
                        ? $"{{\"gen_ai.request.model\":\"{model}\",\"gen_ai.usage.prompt_tokens\":{promptTok},\"gen_ai.usage.completion_tokens\":{outputTok}}}"
                        : "{}";

                    // Railway-Oriented Programming tagging
                    var ropOp = s switch
                    {
                        0 => RailwayOperation.Bind,
                        1 => (RailwayOperation?)null,          // raw LLM call — no ROP op
                        2 => RailwayOperation.Bind,
                        3 => RailwayOperation.TryCatch,
                        _ => RailwayOperation.Then,
                    };
                    var ropSuccess   = !isError;
                    var ropErrorCode = isError
                        ? ErrorMessages[Rng.Next(ErrorMessages.Length)].Split(':')[0].Trim()
                        : null;

                    var span = Span.Create(
                        traceId:         trace.Id,
                        spanId:          s == 0 ? rootSpanId : SpanHex(),
                        parentSpanId:    s == 0 ? null : rootSpanId,
                        name:            spanName,
                        kind:            spanKind,
                        status:          spanStatus,
                        statusMessage:   isError ? ErrorMessages[Rng.Next(ErrorMessages.Length)] : null,
                        startedAt:       spanStart,
                        endedAt:         spanEnd,
                        attributesJson:  attrs,
                        railwayOp:       ropOp,
                        resultIsSuccess: ropOp.HasValue ? ropSuccess : null,
                        railwayErrorCode: ropOp.HasValue && !ropSuccess ? ropErrorCode : null);

                    db.Spans.Add(span);

                    // Seed token_usages for LLM spans
                    if (s == 1)
                    {
                        db.TokenUsages.Add(TokenUsage.Create(
                            spanId:          span.Id,
                            applicationId:   app.Id,
                            model:           model,
                            promptTokens:    promptTok,
                            completionTokens: outputTok,
                            costUsd:         0m)); // cost not recalculated in seeder — zero placeholder
                    }

                    spanStart = spanEnd.AddMilliseconds(Rng.Next(5, 30));
                }
            }
        }

        // ── MetricPoints ──────────────────────────────────────────────────────
        var metricNames = new[]
        {
            ("llm.tokens.input",      "tokens",  MetricKind.Sum),
            ("llm.tokens.output",     "tokens",  MetricKind.Sum),
            ("llm.latency",           "ms",      MetricKind.Histogram),
            ("pipeline.success_rate", "ratio",   MetricKind.Gauge),
            ("agent.executions",      "count",   MetricKind.Sum),
        };

        foreach (var app in new[] { appOrchestrator, appDocBot })
        {
            foreach (var (name, unit, kind) in metricNames)
            {
                for (int w = 0; w < 24; w++)
                {
                    var windowStart = now.AddHours(-w - 1);
                    var baseVal = name switch
                    {
                        "llm.tokens.input"  => Rng.Next(500, 5000),
                        "llm.tokens.output" => Rng.Next(100, 1500),
                        "llm.latency"       => Rng.Next(200, 3000),
                        "pipeline.success_rate" => Rng.NextDouble() * 0.2 + 0.8,
                        _ => Rng.Next(5, 50),
                    };
                    var val = baseVal is double d ? d : (double)(int)baseVal;
                    var min = val * 0.6;
                    var max = val * 1.4;
                    var count = Rng.Next(10, 60);

                    db.MetricPoints.Add(MetricPoint.Create(
                        app.Id, name, unit, kind,
                        "1min", windowStart,
                        min, max, val * count, count));
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static string SpanHex() =>
        Convert.ToHexString(Guid.NewGuid().ToByteArray())[..16].ToLowerInvariant();
}
