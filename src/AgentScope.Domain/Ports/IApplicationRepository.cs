using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

/// <summary>Repository port for <see cref="Application"/> aggregate.</summary>
public interface IApplicationRepository
{
    Task<Result<Application>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<Application>> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Application>>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<Result<Unit>> AddAsync(Application application, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(Application application, CancellationToken ct = default);
}
