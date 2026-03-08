namespace AgentScope.Domain.Services;

/// <summary>Computes the estimated USD cost for a single LLM call.</summary>
public interface ILlmPricingService
{
    /// <summary>
    /// Returns the estimated cost in USD, or <c>null</c> if the model is unknown.
    /// </summary>
    decimal? Calculate(string model, int promptTokens, int completionTokens);
}
