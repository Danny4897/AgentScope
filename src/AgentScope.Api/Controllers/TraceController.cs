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
public sealed class TraceController : ControllerBase
{
    private readonly TraceIngestionAgent _agent;
    private readonly ILogger<TraceController> _logger;

    public TraceController(TraceIngestionAgent agent, ILogger<TraceController> logger)
    {
        _agent  = agent;
        _logger = logger;
    }

    /// <summary>OTLP/HTTP JSON trace export endpoint.</summary>
    [HttpPost("traces")]
    [Consumes("application/json")]
    public async Task<IActionResult> IngestTraces(
        [FromBody] OtlpTraceRequest request,
        CancellationToken ct)
    {
        var app   = HttpContext.Items[ApiKeyMiddleware.ApplicationKey] as Application;
        var owner = HttpContext.Items[ApiKeyMiddleware.OwnerKey] as User;

        if (app is null || owner is null)
            return Unauthorized(new { error = "Authentication context missing." });

        var ctx = AgentContext.Create(AgentCapability.AccessDatabase, ct);
        var result = await _agent.ExecuteAsync(new TraceIngestionInput(request, app, owner), ctx, ct);

        if (!result.IsSuccess)
        {
            var err = result.Error;
            _logger.LogWarning("Trace ingestion failed: {Code} – {Message}", err.Code, err.Message);

            return err.Code == "SUBSCRIPTION_QUOTA_EXCEEDED"
                ? StatusCode(StatusCodes.Status429TooManyRequests, new { error = err.Message })
                : StatusCode(StatusCodes.Status422UnprocessableEntity, new { error = err.Message });
        }

        _logger.LogInformation(
            "Ingested {Traces} traces / {Spans} spans for app {AppId}",
            result.Value.TracesIngested, result.Value.SpansIngested, app.Id);

        return Ok(new
        {
            tracesIngested = result.Value.TracesIngested,
            spansIngested  = result.Value.SpansIngested,
        });
    }
}
