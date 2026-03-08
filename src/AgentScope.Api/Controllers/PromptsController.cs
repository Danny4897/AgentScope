using AgentScope.Api.Middleware;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Domain.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentScope.Api.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record PromptDto(Guid Id, Guid ApplicationId, string Slug, int Version, string Content, string? ChangeNote, bool IsActive, DateTimeOffset PublishedAt);

public record CreatePromptRequest(string Slug, string Content, string? ChangeNote);

public record PublishVersionRequest(string Content, string? ChangeNote);

public record RollbackRequest(int Version);

// ── SDK endpoint (/v1/prompts — API key auth via middleware) ──────────────────

[ApiController]
[Route("v1/prompts")]
public class SdkPromptsController : ControllerBase
{
    private readonly IPromptRepository _repo;

    public SdkPromptsController(IPromptRepository repo) => _repo = repo;

    /// <summary>SDK pull: returns the active prompt content (or a pinned version).</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetPrompt(string slug, [FromQuery] int? version, CancellationToken ct)
    {
        var app = HttpContext.Items[ApiKeyMiddleware.ApplicationKey] as Application;
        if (app is null) return Unauthorized();

        var result = version.HasValue
            ? await _repo.GetVersionAsync(app.Id, slug, version.Value, ct)
            : await _repo.GetActiveAsync(app.Id, slug, ct);

        if (!result.IsSuccess)
            return result.Error.Code == "PROMPT_NO_ACTIVE_VERSION" || result.Error.Code == "PROMPT_VERSION_NOT_FOUND"
                ? NotFound(new { error = result.Error.Message })
                : StatusCode(500, new { error = result.Error.Message });

        var p = result.Value;
        return Ok(new { slug = p.Slug, version = p.Version, content = p.Content, publishedAt = p.PublishedAt });
    }
}

// ── Management endpoints (api/v1/prompts — JWT auth) ─────────────────────────

[ApiController]
[Authorize]
[Route("api/v1/prompts")]
public class PromptsController : ControllerBase
{
    private readonly IPromptRepository _repo;

    public PromptsController(IPromptRepository repo) => _repo = repo;

    private Guid? GetUserId()
    {
        var claim = User.FindFirst("AgentScope_UserId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Lists active prompts for an application.</summary>
    [HttpGet("{appId:guid}")]
    public async Task<IActionResult> List(Guid appId, CancellationToken ct)
    {
        var result = await _repo.ListActiveAsync(appId, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error.Message });
        return Ok(result.Value.Select(ToDto));
    }

    /// <summary>Returns full version history for a slug.</summary>
    [HttpGet("{appId:guid}/{slug}/history")]
    public async Task<IActionResult> History(Guid appId, string slug, CancellationToken ct)
    {
        var result = await _repo.GetVersionHistoryAsync(appId, slug, ct);
        if (!result.IsSuccess) return result.Error.Code == "PROMPT_NOT_FOUND"
            ? NotFound(new { error = result.Error.Message })
            : StatusCode(500, new { error = result.Error.Message });
        return Ok(result.Value.Select(ToDto));
    }

    /// <summary>Creates version 1 of a new slug. Fails if the slug already exists.</summary>
    [HttpPost("{appId:guid}")]
    public async Task<IActionResult> Create(Guid appId, [FromBody] CreatePromptRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = DomainErrors.Prompts.EmptyContent().Message });

        // Conflict check
        var existing = await _repo.ListActiveAsync(appId, ct);
        if (existing.IsSuccess && existing.Value.Any(p => p.Slug == request.Slug.Trim().ToLowerInvariant()))
            return Conflict(new { error = DomainErrors.Prompts.SlugConflict(request.Slug).Message });

        var prompt = Prompt.Create(appId, request.Slug, request.Content, request.ChangeNote);
        var save   = await _repo.SaveAsync(prompt, ct);

        if (!save.IsSuccess) return StatusCode(500, new { error = save.Error.Message });
        return CreatedAtAction(nameof(History), new { appId, slug = prompt.Slug }, ToDto(prompt));
    }

    /// <summary>Publishes a new version of an existing slug.</summary>
    [HttpPut("{appId:guid}/{slug}")]
    public async Task<IActionResult> Publish(Guid appId, string slug, [FromBody] PublishVersionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = DomainErrors.Prompts.EmptyContent().Message });

        var history = await _repo.GetVersionHistoryAsync(appId, slug, ct);
        if (!history.IsSuccess) return NotFound(new { error = history.Error.Message });

        var latestVersion = history.Value.Max(p => p.Version);
        var newPrompt = Prompt.PublishNewVersion(appId, slug, latestVersion, request.Content, request.ChangeNote);
        var save = await _repo.SaveAsync(newPrompt, ct);

        if (!save.IsSuccess) return StatusCode(500, new { error = save.Error.Message });
        return Ok(ToDto(newPrompt));
    }

    /// <summary>Rolls back to a specific version.</summary>
    [HttpPost("{appId:guid}/{slug}/rollback")]
    public async Task<IActionResult> Rollback(Guid appId, string slug, [FromBody] RollbackRequest request, CancellationToken ct)
    {
        var result = await _repo.RollbackAsync(appId, slug, request.Version, ct);
        if (!result.IsSuccess) return result.Error.Code == "PROMPT_VERSION_NOT_FOUND"
            ? NotFound(new { error = result.Error.Message })
            : StatusCode(500, new { error = result.Error.Message });
        return Ok(new { message = $"Rolled back '{slug}' to version {request.Version}." });
    }

    /// <summary>Deletes all versions of a slug.</summary>
    [HttpDelete("{appId:guid}/{slug}")]
    public async Task<IActionResult> Delete(Guid appId, string slug, CancellationToken ct)
    {
        var result = await _repo.DeleteSlugAsync(appId, slug, ct);
        if (!result.IsSuccess) return result.Error.Code == "PROMPT_NOT_FOUND"
            ? NotFound(new { error = result.Error.Message })
            : StatusCode(500, new { error = result.Error.Message });
        return NoContent();
    }

    private static PromptDto ToDto(Prompt p) =>
        new(p.Id, p.ApplicationId, p.Slug, p.Version, p.Content, p.ChangeNote, p.IsActive, p.PublishedAt);
}
