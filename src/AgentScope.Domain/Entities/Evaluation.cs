namespace AgentScope.Domain.Entities;

public sealed class Evaluation
{
    public Guid           Id            { get; private set; }
    public Guid           TraceId       { get; private set; }
    public Guid           ApplicationId { get; private set; }
    public EvalScore      Score         { get; private set; }
    public string?        Label         { get; private set; }
    public string?        Note          { get; private set; }
    public Guid?          ReviewedBy    { get; private set; }
    public DateTimeOffset CreatedAt     { get; private set; }

    private Evaluation() { }

    public static Evaluation Create(
        Guid traceId, Guid applicationId, EvalScore score,
        string? label, string? note, Guid? reviewedBy) => new()
    {
        Id            = Guid.NewGuid(),
        TraceId       = traceId,
        ApplicationId = applicationId,
        Score         = score,
        Label         = label,
        Note          = note,
        ReviewedBy    = reviewedBy,
        CreatedAt     = DateTimeOffset.UtcNow,
    };

    public void Update(EvalScore score, string? label, string? note, Guid? reviewedBy)
    {
        Score      = score;
        Label      = label;
        Note       = note;
        ReviewedBy = reviewedBy;
    }
}

public enum EvalScore { ThumbsDown = -1, Neutral = 0, ThumbsUp = 1 }
