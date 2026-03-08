using System.Text.Json;
using AgentScope.Api.Models.Otlp;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using AgentScope.Domain.Services;
using MonadicSharp;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Persistence.Core;

namespace AgentScope.Api.Agents;

/// <summary>Input for the trace ingestion pipeline.</summary>
public sealed record TraceIngestionInput(
    OtlpTraceRequest Payload,
    Application Application,
    User Owner);

/// <summary>Output: number of spans persisted.</summary>
public sealed record TraceIngestionOutput(int TracesIngested, int SpansIngested);

/// <summary>
/// Parses an OTLP trace payload, enforces the monthly event quota,
/// persists traces + spans, and extracts LLM token usage for cost tracking.
/// </summary>
public sealed class TraceIngestionAgent : IAgent<TraceIngestionInput, TraceIngestionOutput>
{
    private readonly ITraceRepository      _traceRepo;
    private readonly ITokenUsageRepository _tokenUsageRepo;
    private readonly ILlmPricingService    _pricing;
    private readonly IUnitOfWork           _uow;

    public TraceIngestionAgent(
        ITraceRepository      traceRepo,
        ITokenUsageRepository tokenUsageRepo,
        ILlmPricingService    pricing,
        IUnitOfWork           uow)
    {
        _traceRepo      = traceRepo;
        _tokenUsageRepo = tokenUsageRepo;
        _pricing        = pricing;
        _uow            = uow;
    }

    public string Name => "TraceIngestionAgent";

    public AgentCapability RequiredCapabilities => AgentCapability.AccessDatabase;

    public async Task<Result<TraceIngestionOutput>> ExecuteAsync(
        TraceIngestionInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // ── Quota check ──────────────────────────────────────────────────────
        var quota = GetMonthlySpanQuota(input.Owner.Tier);
        if (quota != long.MaxValue)
        {
            var periodStart = new DateTimeOffset(
                DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

            var countResult = await _traceRepo.CountSpansByApplicationAsync(
                input.Application.Id, periodStart, DateTimeOffset.UtcNow, cancellationToken);

            if (!countResult.IsSuccess)
                return Result<TraceIngestionOutput>.Failure(countResult.Error);

            var newSpanCount = input.Payload.ResourceSpans
                .SelectMany(rs => rs.ScopeSpans)
                .SelectMany(ss => ss.Spans)
                .Count();

            if (countResult.Value + newSpanCount > quota)
                return Result<TraceIngestionOutput>.Failure(
                    DomainErrors.Subscriptions.EventQuotaExceeded(quota));
        }

        // ── Parse and persist ─────────────────────────────────────────────────
        var tracesIngested = 0;
        var spansIngested  = 0;

        foreach (var resourceSpan in input.Payload.ResourceSpans)
        {
            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                if (scopeSpan.Spans.Count == 0) continue;

                var byTraceId = scopeSpan.Spans.GroupBy(s => s.TraceId);

                foreach (var traceGroup in byTraceId)
                {
                    var otlpSpans = traceGroup.ToList();

                    var rootSpan  = otlpSpans.FirstOrDefault(s => string.IsNullOrEmpty(s.ParentSpanId))
                                    ?? otlpSpans[0];

                    var startedAt = UnixNanoToDateTimeOffset(rootSpan.StartTimeUnixNano);
                    var trace     = Trace.Create(
                        input.Application.Id,
                        traceGroup.Key,
                        rootSpan.Name,
                        startedAt);

                    // Map spans + extract LLM attributes in one pass
                    var mapped    = otlpSpans.Select(s => MapSpan(trace.Id, s)).ToList();
                    var domainSpans = mapped.Select(x => x.Span).ToList();

                    // Attach spans to the trace aggregate before AddAsync (EF cascade)
                    trace.AttachSpans(domainSpans);

                    var hasError = domainSpans.Any(ds => ds.Status == SpanStatus.Error);
                    var maxEnd   = domainSpans.Max(ds => ds.EndedAt);
                    trace.Complete(
                        hasError ? TraceStatus.Failure : TraceStatus.Success,
                        maxEnd);

                    var addResult = await _traceRepo.AddAsync(trace, cancellationToken);
                    if (!addResult.IsSuccess)
                        return Result<TraceIngestionOutput>.Failure(addResult.Error);

                    // ── Token usage extraction ────────────────────────────────
                    var tokenUsages = mapped
                        .Where(x => x.Llm is not null)
                        .Select(x =>
                        {
                            var cost = _pricing.Calculate(
                                x.Llm!.Model,
                                x.Llm.PromptTokens,
                                x.Llm.CompletionTokens) ?? 0m;

                            return TokenUsage.Create(
                                spanId:          x.Span.Id,
                                applicationId:   input.Application.Id,
                                model:           x.Llm.Model,
                                promptTokens:    x.Llm.PromptTokens,
                                completionTokens: x.Llm.CompletionTokens,
                                costUsd:         cost);
                        })
                        .ToList();

                    if (tokenUsages.Count > 0)
                    {
                        var tokenResult = await _tokenUsageRepo.AddRangeAsync(tokenUsages, cancellationToken);
                        if (!tokenResult.IsSuccess)
                            return Result<TraceIngestionOutput>.Failure(tokenResult.Error);
                    }

                    tracesIngested++;
                    spansIngested += domainSpans.Count;
                }
            }
        }

        var saveResult = await _uow.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
            return Result<TraceIngestionOutput>.Failure(saveResult.Error);

        return Result<TraceIngestionOutput>.Success(new TraceIngestionOutput(tracesIngested, spansIngested));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record LlmAttributes(string Model, int PromptTokens, int CompletionTokens);

    private static (Span Span, LlmAttributes? Llm) MapSpan(Guid domainTraceId, OtlpSpan s)
    {
        var attrs = s.Attributes.ToDictionary(
            kv => kv.Key,
            kv => kv.Value?.ToDisplayString() ?? "");

        var attributesJson = attrs.Count > 0
            ? JsonSerializer.Serialize(attrs)
            : "{}";

        // ── Railway attributes ────────────────────────────────────────────────
        attrs.TryGetValue("agentscope.railway.operation", out var opStr);
        attrs.TryGetValue("agentscope.result.is_success", out var successStr);
        attrs.TryGetValue("agentscope.error.code",        out var errorCode);

        RailwayOperation? railwayOp = opStr is not null
            && Enum.TryParse<RailwayOperation>(opStr, ignoreCase: true, out var parsed)
            ? parsed : null;

        bool? resultIsSuccess = successStr == "true" ? true
                              : successStr == "false" ? false
                              : null;

        var span = Span.Create(
            traceId:         domainTraceId,
            spanId:          s.SpanId,
            parentSpanId:    string.IsNullOrEmpty(s.ParentSpanId) ? null : s.ParentSpanId,
            name:            s.Name,
            kind:            MapKind(s.Kind),
            status:          MapStatus(s.Status?.Code ?? 0),
            statusMessage:   s.Status?.Message,
            startedAt:       UnixNanoToDateTimeOffset(s.StartTimeUnixNano),
            endedAt:         UnixNanoToDateTimeOffset(s.EndTimeUnixNano),
            attributesJson:  attributesJson,
            railwayOp:       railwayOp,
            resultIsSuccess: resultIsSuccess,
            railwayErrorCode: string.IsNullOrEmpty(errorCode) ? null : errorCode);

        // ── LLM token attributes (standard gen_ai.* + legacy fallbacks) ───────
        var model =
            attrs.GetValueOrDefault("gen_ai.request.model") ??
            attrs.GetValueOrDefault("model");

        LlmAttributes? llm = null;
        if (model is not null)
        {
            var promptStr = attrs.GetValueOrDefault("gen_ai.usage.prompt_tokens")
                         ?? attrs.GetValueOrDefault("gen_ai.usage.input_tokens")
                         ?? attrs.GetValueOrDefault("input_tokens") ?? "0";
            var completionStr = attrs.GetValueOrDefault("gen_ai.usage.completion_tokens")
                             ?? attrs.GetValueOrDefault("gen_ai.usage.output_tokens")
                             ?? attrs.GetValueOrDefault("output_tokens") ?? "0";

            if (int.TryParse(promptStr,     out var pt) &&
                int.TryParse(completionStr, out var ct))
                llm = new LlmAttributes(model, pt, ct);
        }

        return (span, llm);
    }

    private static SpanKind MapKind(int kind) => kind switch
    {
        1 => SpanKind.Internal,
        2 => SpanKind.Server,
        3 => SpanKind.Client,
        4 => SpanKind.Producer,
        5 => SpanKind.Consumer,
        _ => SpanKind.Internal,
    };

    private static SpanStatus MapStatus(int code) => code switch
    {
        1 => SpanStatus.Ok,
        2 => SpanStatus.Error,
        _ => SpanStatus.Unset,
    };

    private static DateTimeOffset UnixNanoToDateTimeOffset(string unixNano)
    {
        if (!long.TryParse(unixNano, out var nanos)) return DateTimeOffset.UtcNow;
        return DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000);
    }

    private static long GetMonthlySpanQuota(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free       => 10_000L,
        SubscriptionTier.Pro        => 500_000L,
        SubscriptionTier.Business   => long.MaxValue,
        SubscriptionTier.Enterprise => long.MaxValue,
        _                           => 10_000L,
    };
}
