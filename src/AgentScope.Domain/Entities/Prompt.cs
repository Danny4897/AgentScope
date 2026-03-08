namespace AgentScope.Domain.Entities;

/// <summary>
/// A versioned prompt template managed by the Prompt CMS.
/// Every publish creates a new immutable version row; the active version is
/// tracked via <see cref="ActiveVersion"/> to allow zero-downtime rollback.
/// </summary>
public sealed class Prompt
{
    public Guid   Id              { get; private set; }
    public Guid   ApplicationId   { get; private set; }

    /// <summary>URL-safe identifier, unique per application (e.g. "system-prompt", "rag-query").</summary>
    public string Slug            { get; private set; }

    /// <summary>Monotonically increasing version counter (1-based).</summary>
    public int    Version         { get; private set; }

    /// <summary>Raw prompt text (Markdown or plain).</summary>
    public string Content         { get; private set; }

    /// <summary>Freeform note attached at publish time (e.g. "Added CoT reasoning").</summary>
    public string? ChangeNote     { get; private set; }

    /// <summary><c>true</c> if this version is the one returned by the SDK's GetPromptAsync.</summary>
    public bool   IsActive        { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    private Prompt() { Slug = null!; Content = null!; }

    /// <summary>Creates version 1 of a new slug.</summary>
    public static Prompt Create(Guid applicationId, string slug, string content, string? changeNote = null) =>
        new()
        {
            Id            = Guid.NewGuid(),
            ApplicationId = applicationId,
            Slug          = slug.Trim().ToLowerInvariant(),
            Version       = 1,
            Content       = content,
            ChangeNote    = changeNote,
            IsActive      = true,
            PublishedAt   = DateTimeOffset.UtcNow,
        };

    /// <summary>Creates the next version from an existing prompt. The caller must deactivate previous rows.</summary>
    public static Prompt PublishNewVersion(Guid applicationId, string slug, int previousVersion, string content, string? changeNote = null) =>
        new()
        {
            Id            = Guid.NewGuid(),
            ApplicationId = applicationId,
            Slug          = slug.Trim().ToLowerInvariant(),
            Version       = previousVersion + 1,
            Content       = content,
            ChangeNote    = changeNote,
            IsActive      = true,
            PublishedAt   = DateTimeOffset.UtcNow,
        };

    /// <summary>Marks this version as the active one (used during rollback).</summary>
    public void Activate()   => IsActive = true;

    /// <summary>Marks this version as inactive (called when a newer version is published).</summary>
    public void Deactivate() => IsActive = false;
}
