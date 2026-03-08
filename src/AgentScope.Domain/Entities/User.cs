namespace AgentScope.Domain.Entities;

/// <summary>
/// Domain user entity.  Authentication identity is owned by ASP.NET Core Identity;
/// this entity captures business-level profile data and links to Subscription.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public SubscriptionTier Tier { get; private set; }
    public string? SshPublicKey { get; private set; }
    public string? GitHubId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public IReadOnlyCollection<Application> Applications => _applications.AsReadOnly();
    private readonly List<Application> _applications = [];

    private User() { Email = null!; DisplayName = null!; }

    public static User Create(string email, string displayName) =>
        new()
        {
            Id          = Guid.NewGuid(),
            Email       = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Tier        = SubscriptionTier.Free,
            CreatedAt   = DateTimeOffset.UtcNow,
        };

    public void UpgradeTier(SubscriptionTier tier) => Tier = tier;

    public static User CreateWithSsh(string sshPublicKey, string displayName) =>
        new()
        {
            Id           = Guid.NewGuid(),
            Email        = $"ssh_{Guid.NewGuid():N}@agentscope.local",
            DisplayName  = displayName.Trim(),
            SshPublicKey = sshPublicKey.Trim(),
            Tier         = SubscriptionTier.Free,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

    public static User CreateWithGitHub(string gitHubId, string email, string displayName) =>
        new()
        {
            Id          = Guid.NewGuid(),
            Email       = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            GitHubId    = gitHubId,
            Tier        = SubscriptionTier.Free,
            CreatedAt   = DateTimeOffset.UtcNow,
        };

    public void SetSshPublicKey(string key) => SshPublicKey = key.Trim();
    public void SetGitHubId(string id) => GitHubId = id;
}

public enum SubscriptionTier { Free, Pro, Business, Enterprise }
