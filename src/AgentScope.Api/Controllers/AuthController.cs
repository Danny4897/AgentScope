using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentScope.Domain.Entities;
using AgentScope.Domain.Errors;
using AgentScope.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MonadicSharp;

namespace AgentScope.Api.Controllers;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string Email, string DisplayName);

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AgentScopeDbContext _dbContext;
    private readonly IConfiguration _config;

    public AuthController(
        UserManager<IdentityUser> userManager,
        AgentScopeDbContext dbContext,
        IConfiguration config)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await RegisterCore(request);
        return result.Match<IActionResult>(
            success => Ok(success),
            failure => BadRequest(new { error = failure.Message, code = failure.Code })
        );
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await LoginCore(request);
        return result.Match<IActionResult>(
            success => Ok(success),
            failure => Unauthorized(new { error = failure.Message, code = failure.Code })
        );
    }

    private async Task<Result<AuthResponse>> RegisterCore(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return DomainErrors.Auth.EmailInUse();

        // 1. Create Identity User
        var identityUser = new IdentityUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(identityUser, request.Password);
        
        if (!result.Succeeded)
            return DomainErrors.Auth.CreationError(string.Join("; ", result.Errors.Select(e => e.Description)));

        // 2. Create Domain User
        var domainUser = AgentScope.Domain.Entities.User.Create(request.Email, request.DisplayName);
        _dbContext.Users.Add(domainUser);
        await _dbContext.SaveChangesAsync();

        // 3. Generate Token
        var token = GenerateJwtToken(identityUser, domainUser);

        return new AuthResponse(token, domainUser.Email, domainUser.DisplayName);

        //Scritto con MonadicSharp per mostrare come si possono gestire i flussi di successo/errore senza eccezioni
    }

    private async Task<Result<AuthResponse>> LoginCore(LoginRequest request)
    {
        var identityUser = await _userManager.FindByEmailAsync(request.Email);
        if (identityUser == null || !await _userManager.CheckPasswordAsync(identityUser, request.Password))
            return DomainErrors.Auth.InvalidCredentials();

        // Fetch Domain User
        var domainUser = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (domainUser == null)
            return DomainErrors.Auth.UserNotFound();

        var token = GenerateJwtToken(identityUser, domainUser);

        return new AuthResponse(token, domainUser.Email, domainUser.DisplayName);
    }

    private string GenerateJwtToken(IdentityUser identityUser, User domainUser)
    {
        var key = _config["Jwt:Key"] ?? "SuperSecretKeyForDevelopmentOnly1234567890!";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, identityUser.Id),
            new Claim(JwtRegisteredClaimNames.Email, identityUser.Email!),
            new Claim("AgentScope_UserId", domainUser.Id.ToString()),
            new Claim("AgentScope_Tier", domainUser.Tier.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}