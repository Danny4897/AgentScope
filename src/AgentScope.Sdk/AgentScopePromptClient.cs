using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AgentScope.Sdk;

/// <summary>
/// Fetches versioned prompts from the AgentScope Prompt CMS.
/// Register via <see cref="AgentScopeCollector.AddAgentScope"/> — the client
/// is automatically wired as a scoped service.
/// </summary>
/// <example>
/// <code>
/// // Inject in your handler:
/// public class MyAgent(AgentScopePromptClient prompts)
/// {
///     public async Task RunAsync()
///     {
///         var systemPrompt = await prompts.GetAsync("system-prompt");
///     }
/// }
/// </code>
/// </example>
public sealed class AgentScopePromptClient
{
    private readonly HttpClient _http;
    private readonly AgentScopeOptions _options;

    public AgentScopePromptClient(HttpClient http, AgentScopeOptions options)
    {
        _http    = http;
        _options = options;
    }

    /// <summary>
    /// Fetches the active prompt for <paramref name="slug"/>.
    /// Pass <paramref name="version"/> to retrieve a pinned historical version.
    /// </summary>
    /// <returns>The prompt content string, or <c>null</c> if not found (404).</returns>
    public async Task<string?> GetAsync(string slug, int? version = null, CancellationToken ct = default)
    {
        var url = $"{_options.Endpoint.TrimEnd('/')}/v1/prompts/{Uri.EscapeDataString(slug)}";
        if (version.HasValue) url += $"?version={version.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _options.ApiKey);

        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PromptResponse>(cancellationToken: ct);
        return body?.Content;
    }

    private sealed record PromptResponse(string Slug, int Version, string Content, DateTimeOffset PublishedAt);
}

/// <summary>Extension on <see cref="IServiceCollection"/> to register the prompt client.</summary>
internal static class PromptClientServiceExtensions
{
    internal static IServiceCollection AddPromptClient(this IServiceCollection services, AgentScopeOptions options)
    {
        services.AddHttpClient<AgentScopePromptClient>(client =>
        {
            client.BaseAddress = new Uri(options.Endpoint.TrimEnd('/'));
        });

        return services;
    }
}
