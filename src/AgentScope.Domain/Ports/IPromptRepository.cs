using AgentScope.Domain.Entities;
using MonadicSharp;

namespace AgentScope.Domain.Ports;

public interface IPromptRepository
{
    /// <summary>Returns the active (latest published) version for a slug.</summary>
    Task<Result<Prompt>> GetActiveAsync(Guid applicationId, string slug, CancellationToken ct = default);

    /// <summary>Returns a specific pinned version.</summary>
    Task<Result<Prompt>> GetVersionAsync(Guid applicationId, string slug, int version, CancellationToken ct = default);

    /// <summary>Lists all distinct slugs with their active version for an application.</summary>
    Task<Result<IReadOnlyList<Prompt>>> ListActiveAsync(Guid applicationId, CancellationToken ct = default);

    /// <summary>Returns all versions for a slug (for the diff/history view).</summary>
    Task<Result<IReadOnlyList<Prompt>>> GetVersionHistoryAsync(Guid applicationId, string slug, CancellationToken ct = default);

    /// <summary>Persists a new <see cref="Prompt"/> row and deactivates the previous active version.</summary>
    Task<Result<Unit>> SaveAsync(Prompt prompt, CancellationToken ct = default);

    /// <summary>Sets <paramref name="version"/> as active and deactivates all other versions of the slug.</summary>
    Task<Result<Unit>> RollbackAsync(Guid applicationId, string slug, int version, CancellationToken ct = default);

    /// <summary>Deletes all versions of a slug (soft-deletes via IsActive = false is intentional here — use sparingly).</summary>
    Task<Result<Unit>> DeleteSlugAsync(Guid applicationId, string slug, CancellationToken ct = default);
}
