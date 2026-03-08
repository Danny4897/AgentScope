namespace AgentScope.Domain.Entities;

/// <summary>
/// Records LLM token consumption and estimated USD cost for a single span.
/// Computed at ingestion time from OTLP <c>gen_ai.*</c> semantic attributes.
/// </summary>
public sealed class TokenUsage
{
    public Guid   Id              { get; private set; }
    public Guid   SpanId          { get; private set; }
    public Guid   ApplicationId   { get; private set; }
    public string Model           { get; private set; }
    public int    PromptTokens    { get; private set; }
    public int    CompletionTokens { get; private set; }
    public int    TotalTokens     => PromptTokens + CompletionTokens;
    public decimal CostUsd        { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private TokenUsage() { Model = null!; }

    public static TokenUsage Create(
        Guid   spanId,
        Guid   applicationId,
        string model,
        int    promptTokens,
        int    completionTokens,
        decimal costUsd) =>
        new()
        {
            Id               = Guid.NewGuid(),
            SpanId           = spanId,
            ApplicationId    = applicationId,
            Model            = model,
            PromptTokens     = promptTokens,
            CompletionTokens = completionTokens,
            CostUsd          = costUsd,
            RecordedAt       = DateTimeOffset.UtcNow,
        };
}
