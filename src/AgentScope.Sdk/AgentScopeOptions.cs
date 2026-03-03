namespace AgentScope.Sdk;

/// <summary>Configuration for the AgentScope SDK collector.</summary>
public sealed class AgentScopeOptions
{
    /// <summary>AgentScope ingest endpoint (e.g. https://app.agentscope.io).</summary>
    public string Endpoint { get; set; } = "https://app.agentscope.io";

    /// <summary>API key from the AgentScope dashboard.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Name of the service being instrumented.</summary>
    public string ServiceName { get; set; } = "my-service";

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("AgentScopeOptions.ApiKey must be set.", nameof(ApiKey));
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new ArgumentException("AgentScopeOptions.Endpoint must be set.", nameof(Endpoint));
    }
}
