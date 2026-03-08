using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace AgentScope.Sdk;

/// <summary>
/// Two-line SDK integration: instrument any OpenTelemetry-compatible .NET app
/// to send traces and metrics to AgentScope.
/// </summary>
/// <example>
/// <code>
/// // In Program.cs or Startup.cs:
/// builder.Services.AddAgentScope(options =>
/// {
///     options.Endpoint = "https://app.agentscope.io";
///     options.ApiKey   = Environment.GetEnvironmentVariable("AGENTSCOPE_API_KEY")!;
/// });
/// </code>
/// </example>
public static class AgentScopeCollector
{
    /// <summary>Registers AgentScope OTLP exporter into the DI container.</summary>
    public static IServiceCollection AddAgentScope(
        this IServiceCollection services,
        Action<AgentScopeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AgentScopeOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddPromptClient(options);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(options.ServiceName)
                    .AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri($"{options.Endpoint.TrimEnd('/')}/v1/traces");
                        otlp.Headers  = $"x-api-key={options.ApiKey}";
                    });
            })
            .WithMetrics(metrics =>
            {
                // OTLP metrics export wired via MonadicSharp.Telemetry push-collector.
                metrics.AddMeter(options.ServiceName);
            });

        return services;
    }
}
