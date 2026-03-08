using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MonadicSharp;
using System.Text.Json;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class PromptRepository : IPromptRepository
{
    private readonly AgentScopeDbContext _db;
    private readonly IDistributedCache _cache;

    public PromptRepository(AgentScopeDbContext db, IDistributedCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    // ── Cache helpers ────────────────────────────────────────────────────────

    private static string ActiveKey(Guid appId, string slug) => $"prompt:{appId}:{slug}:active";

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    private async Task InvalidateCacheAsync(Guid appId, string slug, CancellationToken ct)
        => await _cache.RemoveAsync(ActiveKey(appId, slug), ct);

    // ── Read operations ──────────────────────────────────────────────────────

    public async Task<Result<Prompt>> GetActiveAsync(Guid applicationId, string slug, CancellationToken ct = default)
    {
        var cacheKey = ActiveKey(applicationId, slug);
        var cached   = await _cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            var hit = JsonSerializer.Deserialize<Prompt>(cached);
            if (hit is not null) return Result<Prompt>.Success(hit);
        }

        var prompt = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug && p.IsActive)
            .FirstOrDefaultAsync(ct);

        if (prompt is null)
            return Result<Prompt>.Failure(DomainErrors.Prompts.NoActiveVersion(slug));

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(prompt), CacheOptions, ct);
        return Result<Prompt>.Success(prompt);
    }

    public async Task<Result<Prompt>> GetVersionAsync(Guid applicationId, string slug, int version, CancellationToken ct = default)
    {
        var prompt = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug && p.Version == version)
            .FirstOrDefaultAsync(ct);

        return prompt is null
            ? Result<Prompt>.Failure(DomainErrors.Prompts.VersionNotFound(slug, version))
            : Result<Prompt>.Success(prompt);
    }

    public async Task<Result<IReadOnlyList<Prompt>>> ListActiveAsync(Guid applicationId, CancellationToken ct = default)
    {
        var rows = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.IsActive)
            .OrderBy(p => p.Slug)
            .ToListAsync(ct);

        return Result<IReadOnlyList<Prompt>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<Prompt>>> GetVersionHistoryAsync(Guid applicationId, string slug, CancellationToken ct = default)
    {
        var rows = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug)
            .OrderByDescending(p => p.Version)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Result<IReadOnlyList<Prompt>>.Failure(DomainErrors.Prompts.NotFound(slug));

        return Result<IReadOnlyList<Prompt>>.Success(rows);
    }

    // ── Write operations ─────────────────────────────────────────────────────

    public async Task<Result<Unit>> SaveAsync(Prompt prompt, CancellationToken ct = default)
    {
        // Deactivate all previous versions of this slug
        var previous = await _db.Prompts
            .Where(p => p.ApplicationId == prompt.ApplicationId && p.Slug == prompt.Slug && p.IsActive)
            .ToListAsync(ct);

        foreach (var p in previous) p.Deactivate();

        await _db.Prompts.AddAsync(prompt, ct);
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(prompt.ApplicationId, prompt.Slug, ct);

        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Unit>> RollbackAsync(Guid applicationId, string slug, int version, CancellationToken ct = default)
    {
        var target = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug && p.Version == version)
            .FirstOrDefaultAsync(ct);

        if (target is null)
            return Result<Unit>.Failure(DomainErrors.Prompts.VersionNotFound(slug, version));

        var allVersions = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug)
            .ToListAsync(ct);

        foreach (var p in allVersions)
        {
            if (p.Version == version) p.Activate();
            else                      p.Deactivate();
        }

        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(applicationId, slug, ct);

        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Unit>> DeleteSlugAsync(Guid applicationId, string slug, CancellationToken ct = default)
    {
        var rows = await _db.Prompts
            .Where(p => p.ApplicationId == applicationId && p.Slug == slug)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Result<Unit>.Failure(DomainErrors.Prompts.NotFound(slug));

        _db.Prompts.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        await InvalidateCacheAsync(applicationId, slug, ct);

        return Result<Unit>.Success(Unit.Value);
    }
}
