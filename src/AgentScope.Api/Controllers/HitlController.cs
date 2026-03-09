using AgentScope.Api.Middleware;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentScope.Api.Controllers;

public record HitlReviewDto(Guid Id, Guid TraceId, Guid ApplicationId, string Action, string? Payload, HitlStatus Status, string? ReviewNote, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt);
public record CreateHitlRequest(Guid TraceId, string Action, string? Payload);
public record ReviewHitlRequest(HitlStatus Status, string? Note);

// ── SDK endpoints (v1/hitl — API key auth) ────────────────────────────────────
[ApiController]
[Route("v1/hitl")]
public sealed class SdkHitlController : ControllerBase
{
    private readonly IHitlReviewRepository _repo;
    public SdkHitlController(IHitlReviewRepository repo) => _repo = repo;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHitlRequest req, CancellationToken ct)
    {
        var app = HttpContext.Items[ApiKeyMiddleware.ApplicationKey] as Application;
        if (app is null) return Unauthorized();
        var review = HitlReview.Create(req.TraceId, app.Id, req.Action, req.Payload);
        var result = await _repo.CreateAsync(review, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return CreatedAtAction(nameof(GetStatus), new { id = review.Id }, ToDto(review));
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var app = HttpContext.Items[ApiKeyMiddleware.ApplicationKey] as Application;
        if (app is null) return Unauthorized();
        var result = await _repo.GetAsync(id, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        if (result.Value is null || result.Value.ApplicationId != app.Id) return NotFound();
        return Ok(new { id = result.Value.Id, status = result.Value.Status.ToString() });
    }

    private static HitlReviewDto ToDto(HitlReview h) =>
        new(h.Id, h.TraceId, h.ApplicationId, h.Action, h.Payload, h.Status, h.ReviewNote, h.CreatedAt, h.ReviewedAt);
}

// ── Management endpoints (api/v1/hitl — JWT auth) ─────────────────────────────
[ApiController]
[Authorize]
[Route("api/v1/hitl")]
public sealed class HitlManagementController : ControllerBase
{
    private readonly IHitlReviewRepository _repo;
    public HitlManagementController(IHitlReviewRepository repo) => _repo = repo;

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("AgentScope_UserId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    [HttpGet("pending/{appId:guid}")]
    public async Task<IActionResult> GetPending(Guid appId, CancellationToken ct)
    {
        var result = await _repo.GetPendingAsync(appId, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return Ok(result.Value.Select(ToDto));
    }

    [HttpPut("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewHitlRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _repo.ReviewAsync(id, req.Status, req.Note, userId, ct);
        if (!result.IsSuccess) return result.Error.Code == "HITL_NOT_FOUND"
            ? NotFound(new { error = result.Error.Message })
            : StatusCode(500, new { error = result.Error.Message });
        return NoContent();
    }

    private static HitlReviewDto ToDto(HitlReview h) =>
        new(h.Id, h.TraceId, h.ApplicationId, h.Action, h.Payload, h.Status, h.ReviewNote, h.CreatedAt, h.ReviewedAt);
}
