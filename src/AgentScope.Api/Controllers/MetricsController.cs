using AgentScope.Api.Agents;
using AgentScope.Api.Middleware;
using AgentScope.Api.Models.Otlp;
using AgentScope.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;

namespace AgentScope.Api.Controllers;

[ApiController]
[Route("v1")]
public sealed class MetricsController : ControllerBase
{
    private readonly MetricsAggregationAgent _agent;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(MetricsAggregationAgent agent, ILogger<MetricsController> logger)
    {
        _agent  = agent;
        _logger = logger;
    }

    /// <summary>OTLP/HTTP JSON metrics export endpoint.</summary>
    [HttpPost("metrics")]
    [Consumes("application/json")]
    public async Task<IActionResult> IngestMetrics(
        [FromBody] OtlpMetricsRequest request,
        CancellationToken ct)
    {
        var app   = HttpContext.Items[ApiKeyMiddleware.ApplicationKey] as Application;
        var owner = HttpContext.Items[ApiKeyMiddleware.OwnerKey] as User;

        if (app is null || owner is null)
            return Unauthorized(new { error = "Authentication context missing." });

        var ctx = AgentContext.Create(AgentCapability.AccessDatabase, ct);
        var result = await _agent.ExecuteAsync(new MetricsIngestionInput(request, app, owner), ctx, ct);

        if (!result.IsSuccess)
        {
            var err = result.Error;
            _logger.LogWarning("Metrics ingestion failed: {Code} – {Message}", err.Code, err.Message);

            return err.Code == "SUBSCRIPTION_QUOTA_EXCEEDED"
                ? StatusCode(StatusCodes.Status429TooManyRequests, new { error = err.Message })
                : StatusCode(StatusCodes.Status422UnprocessableEntity, new { error = err.Message });
        }

        _logger.LogInformation(
            "Ingested {Points} metric points for app {AppId}",
            result.Value.MetricPointsIngested, app.Id);

        return Ok(new { metricPointsIngested = result.Value.MetricPointsIngested });
    }
}
