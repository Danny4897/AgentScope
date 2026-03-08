using System.Security.Claims;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonadicSharp.Persistence.Core;

namespace AgentScope.Api.Controllers;

public record CreateAppRequest(string Name, string? Description);

public record AppDto(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt, string? Description);
public record AppCreatedDto(Guid Id, string Name, string ApiKey, bool IsActive, DateTimeOffset CreatedAt, string? Description);

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class AppsController : ControllerBase
{
    private readonly IApplicationRepository _appRepo;
    private readonly IUnitOfWork _uow;

    public AppsController(IApplicationRepository appRepo, IUnitOfWork uow)
    {
        _appRepo = appRepo;
        _uow     = uow;
    }

    [HttpGet]
    public async Task<IActionResult> GetApps(CancellationToken ct)
    {
        var userId = GetAgentScopeUserId();
        if (userId == null)
            return Unauthorized(new { error = "Missing AgentScope_UserId claim." });

        var result = await _appRepo.GetByOwnerAsync(userId.Value, ct);
        if (!result.IsSuccess)
            return StatusCode(500, new { error = result.Error.Message });

        var dtos = result.Value.Select(a => new AppDto(a.Id, a.Name, a.IsActive, a.CreatedAt, a.Description));
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> CreateApp([FromBody] CreateAppRequest request, CancellationToken ct)
    {
        var userId = GetAgentScopeUserId();
        if (userId == null)
            return Unauthorized(new { error = "Missing AgentScope_UserId claim." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        var apiKey = GenerateApiKey();
        var app = Application.Create(userId.Value, request.Name, apiKey, request.Description);

        var addResult = await _appRepo.AddAsync(app, ct);
        if (!addResult.IsSuccess)
            return StatusCode(500, new { error = addResult.Error.Message });

        await _uow.SaveChangesAsync(ct);

        return Ok(new AppCreatedDto(app.Id, app.Name, apiKey, app.IsActive, app.CreatedAt, app.Description));
    }

    private Guid? GetAgentScopeUserId()
    {
        var claim = User.FindFirst("AgentScope_UserId");
        if (claim == null || !Guid.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return "sk_live_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
