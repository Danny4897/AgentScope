using AgentScope.Domain.Services;

namespace AgentScope.Infrastructure.Services;

/// <summary>
/// Static pricing table based on publicly available API pricing (USD per 1 000 tokens).
/// Update prices here as vendors change their tariffs.
/// </summary>
public sealed class LlmPricingService : ILlmPricingService
{
    // (InputPer1K, OutputPer1K) in USD
    private static readonly Dictionary<string, (decimal Input, decimal Output)> Prices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "gpt-4o",            (0.0025m,  0.0100m) },
            { "gpt-4o-mini",       (0.00015m, 0.0006m) },
            { "gpt-4-turbo",       (0.0100m,  0.0300m) },
            { "gpt-3.5-turbo",     (0.0005m,  0.0015m) },
            { "claude-3-5-sonnet", (0.0030m,  0.0150m) },
            { "claude-3-5-haiku",  (0.0008m,  0.0040m) },
            { "claude-3-opus",     (0.0150m,  0.0750m) },
            { "gemini-1.5-pro",    (0.00125m, 0.0050m) },
            { "gemini-1.5-flash",  (0.000075m,0.0003m) },
            { "gemini-2.0-flash",  (0.0001m,  0.0004m) },
        };

    public decimal? Calculate(string model, int promptTokens, int completionTokens)
    {
        var key = FindKey(model);
        if (key is null) return null;

        var (input, output) = Prices[key];
        return (promptTokens / 1000m * input) + (completionTokens / 1000m * output);
    }

    // Longest-match wins so "gpt-4o-mini" beats "gpt-4o".
    private static string? FindKey(string model) =>
        Prices.Keys
              .Where(k => model.Contains(k, StringComparison.OrdinalIgnoreCase))
              .MaxBy(k => k.Length);
}
