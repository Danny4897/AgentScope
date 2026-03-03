using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using MonadicSharp;

namespace AgentScope.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AgentScopeDbContext _db;

    public UserRepository(AgentScopeDbContext db) => _db = db;

    public async Task<Result<User>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([id], ct);
        return user is null
            ? Result<User>.Failure(DomainErrors.Users.NotFound(id))
            : Result<User>.Success(user);
    }

    public async Task<Result<User>> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        // Email is stored already lowercased (see User.Create). Normalise the
        // input here so the query works regardless of caller casing.
        var normalised = email.ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalised, ct);
        return user is null
            ? Result<User>.Failure(DomainErrors.Users.NotFound(Guid.Empty))
            : Result<User>.Success(user);
    }

    public async Task<Result<Unit>> AddAsync(User user, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);
        return Result<Unit>.Success(Unit.Value);
    }

    public Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }
}
