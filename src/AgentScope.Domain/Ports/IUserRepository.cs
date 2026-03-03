using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

/// <summary>Repository port for <see cref="User"/>.</summary>
public interface IUserRepository
{
    Task<Result<User>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<User>> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Result<Unit>> AddAsync(User user, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default);
}
