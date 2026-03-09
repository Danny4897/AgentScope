namespace AgentScope.Domain.Entities;

public enum HitlStatus { Pending, Approved, Rejected }

public sealed class HitlReview
{
    public Guid           Id            { get; private set; }
    public Guid           TraceId       { get; private set; }
    public Guid           ApplicationId { get; private set; }
    public string         Action        { get; private set; } = string.Empty;
    public string?        Payload       { get; private set; }
    public HitlStatus     Status        { get; private set; }
    public string?        ReviewNote    { get; private set; }
    public Guid?          ReviewedBy    { get; private set; }
    public DateTimeOffset CreatedAt     { get; private set; }
    public DateTimeOffset? ReviewedAt   { get; private set; }

    private HitlReview() { }

    public static HitlReview Create(Guid traceId, Guid applicationId, string action, string? payload) => new()
    {
        Id            = Guid.NewGuid(),
        TraceId       = traceId,
        ApplicationId = applicationId,
        Action        = action,
        Payload       = payload,
        Status        = HitlStatus.Pending,
        CreatedAt     = DateTimeOffset.UtcNow,
    };

    public void Review(HitlStatus status, string? note, Guid? reviewedBy)
    {
        Status     = status;
        ReviewNote = note;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}
