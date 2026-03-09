using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentScope.Api.Controllers;

public record EvalDto(Guid Id, Guid TraceId, Guid ApplicationId, EvalScore Score, string? Label, string? Note, DateTimeOffset CreatedAt);
public record SaveEvalRequest(Guid TraceId, Guid ApplicationId, EvalScore Score, string? Label, string? Note);

[ApiController]
[Authorize]
[Route("api/v1/evals")]
public sealed class EvaluationsController : ControllerBase
{
    private readonly IEvaluationRepository _repo;
    public EvaluationsController(IEvaluationRepository repo) => _repo = repo;

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("AgentScope_UserId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Save or update evaluation for a trace.</summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveEvalRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        var eval   = Evaluation.Create(req.TraceId, req.ApplicationId, req.Score, req.Label, req.Note, userId);
        var result = await _repo.SaveAsync(eval, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return Ok(ToDto(eval));
    }

    /// <summary>Get evaluation for a specific trace.</summary>
    [HttpGet("trace/{traceId:guid}")]
    public async Task<IActionResult> GetByTrace(Guid traceId, CancellationToken ct)
    {
        var result = await _repo.GetByTraceAsync(traceId, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return result.Value is null ? NotFound() : Ok(ToDto(result.Value));
    }

    /// <summary>Get aggregate evaluation stats for an application.</summary>
    [HttpGet("aggregate/{appId:guid}")]
    public async Task<IActionResult> GetAggregate(Guid appId, CancellationToken ct)
    {
        var result = await _repo.GetAggregateAsync(appId, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return Ok(result.Value);
    }

    private static EvalDto ToDto(Evaluation e) =>
        new(e.Id, e.TraceId, e.ApplicationId, e.Score, e.Label, e.Note, e.CreatedAt);
}
