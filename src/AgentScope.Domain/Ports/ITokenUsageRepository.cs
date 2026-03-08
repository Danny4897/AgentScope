using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

public sealed record CostSummary(
    decimal TotalCostUsd,
    long    TotalTokens,
    long    PromptTokens,
    long    CompletionTokens,
    IReadOnlyDictionary<string, decimal> CostByModel);

public sealed record ModelUsageRow(
    string  Model,
    long    PromptTokens,
    long    CompletionTokens,
    decimal CostUsd);

public interface ITokenUsageRepository
{
    Task<Result<Unit>> AddRangeAsync(
        IEnumerable<TokenUsage> usages,
        CancellationToken ct = default);

    Task<Result<CostSummary>> GetCostSummaryAsync(
        Guid?          applicationId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<ModelUsageRow>>> GetBreakdownByModelAsync(
        Guid?          applicationId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct = default);
}
