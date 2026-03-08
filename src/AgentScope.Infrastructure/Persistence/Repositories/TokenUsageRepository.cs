using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class TokenUsageRepository : ITokenUsageRepository
{
    private readonly AgentScopeDbContext _db;

    public TokenUsageRepository(AgentScopeDbContext db) => _db = db;

    public Task<Result<Unit>> AddRangeAsync(IEnumerable<TokenUsage> usages, CancellationToken ct = default)
    {
        _db.TokenUsages.AddRange(usages);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    public async Task<Result<CostSummary>> GetCostSummaryAsync(
        Guid? applicationId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct = default)
    {
        var query = _db.TokenUsages.Where(t => t.RecordedAt >= from && t.RecordedAt <= until);
        if (applicationId.HasValue)
            query = query.Where(t => t.ApplicationId == applicationId.Value);

        var rows = await query.ToListAsync(ct);

        IReadOnlyDictionary<string, decimal> byModel = rows
            .GroupBy(r => r.Model, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(r => r.CostUsd),
                StringComparer.OrdinalIgnoreCase);

        return Result<CostSummary>.Success(new CostSummary(
            TotalCostUsd:     rows.Sum(r => r.CostUsd),
            TotalTokens:      rows.Sum(r => (long)r.TotalTokens),
            PromptTokens:     rows.Sum(r => (long)r.PromptTokens),
            CompletionTokens: rows.Sum(r => (long)r.CompletionTokens),
            CostByModel:      byModel));
    }

    public async Task<Result<IReadOnlyList<ModelUsageRow>>> GetBreakdownByModelAsync(
        Guid? applicationId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct = default)
    {
        var query = _db.TokenUsages.Where(t => t.RecordedAt >= from && t.RecordedAt <= until);
        if (applicationId.HasValue)
            query = query.Where(t => t.ApplicationId == applicationId.Value);

        var rows = await query
            .GroupBy(t => t.Model)
            .Select(g => new
            {
                Model            = g.Key,
                PromptTokens     = (long)g.Sum(t => t.PromptTokens),
                CompletionTokens = (long)g.Sum(t => t.CompletionTokens),
                CostUsd          = g.Sum(t => t.CostUsd),
            })
            .OrderByDescending(r => r.CostUsd)
            .ToListAsync(ct);

        IReadOnlyList<ModelUsageRow> result = rows
            .Select(r => new ModelUsageRow(r.Model, r.PromptTokens, r.CompletionTokens, r.CostUsd))
            .ToList();

        return Result<IReadOnlyList<ModelUsageRow>>.Success(result);
    }
}
