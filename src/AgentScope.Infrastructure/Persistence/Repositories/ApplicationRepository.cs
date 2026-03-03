using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly AgentScopeDbContext _db;

    public ApplicationRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<Application>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var app = await _db.Applications.FindAsync([id], ct);
        return app is null
            ? Result<Application>.Failure(DomainErrors.Applications.NotFound(id))
            : Result<Application>.Success(app);
    }

    public async Task<Result<Application>> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var app = await _db.Applications
            .FirstOrDefaultAsync(a => a.ApiKey == apiKey && a.IsActive, ct);
        return app is null
            ? Result<Application>.Failure(DomainErrors.Applications.NotFound(Guid.Empty))
            : Result<Application>.Success(app);
    }

    public async Task<Result<IReadOnlyList<Application>>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        var apps = await _db.Applications
            .Where(a => a.OwnerId == ownerId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return Result<IReadOnlyList<Application>>.Success(apps);
    }

    public async Task<Result<Unit>> AddAsync(Application application, CancellationToken ct = default)
    {
        await _db.Applications.AddAsync(application, ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public Task<Result<Unit>> UpdateAsync(Application application, CancellationToken ct = default)
    {
        _db.Applications.Update(application);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }
}
