namespace AgentScope.Domain.Entities;

/// <summary>
/// A monitored application registered by a user in AgentScope.
/// One application = one SDK api-key. Multiple applications per account.
/// </summary>
public sealed class Application
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }           // FK → User.Id
    public string Name { get; private set; }
    public string ApiKey { get; private set; }          // hashed on read
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Navigation
    public IReadOnlyCollection<Trace> Traces => _traces.AsReadOnly();
    private readonly List<Trace> _traces = [];

    private Application() { Name = null!; ApiKey = null!; } // EF

    public static Application Create(Guid ownerId, string name, string apiKey, string? description = null) =>
        new()
        {
            Id          = Guid.NewGuid(),
            OwnerId     = ownerId,
            Name        = name.Trim(),
            ApiKey      = apiKey,
            Description = description?.Trim(),
            IsActive    = true,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow,
        };

    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Rename(string name) { Name = name.Trim(); UpdatedAt = DateTimeOffset.UtcNow; }
}
