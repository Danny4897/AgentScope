using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AgentScope.Api.Controllers;

public record SshChallengeRequest(string Identity);
public record SshChallengeResponse(string Nonce, string Command);
public record SshVerifyRequest(string Identity, string Signature);
public record SshRegisterRequest(string DisplayName, string SshPublicKey);

[ApiController]
[Route("api/v1/auth/ssh")]
public class SshAuthController : ControllerBase
{
    private readonly SshChallengeStore _challenges;
    private readonly AgentScopeDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<SshAuthController> _logger;

    public SshAuthController(
        SshChallengeStore challenges,
        AgentScopeDbContext db,
        UserManager<IdentityUser> userManager,
        IConfiguration config,
        ILogger<SshAuthController> logger)
    {
        _challenges  = challenges;
        _db          = db;
        _userManager = userManager;
        _config      = config;
        _logger      = logger;
    }

    /// <summary>Register a new user with an SSH public key.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] SshRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SshPublicKey))
            return BadRequest(new { error = "SSH public key is required." });

        var key = request.SshPublicKey.Trim();
        if (!key.StartsWith("ssh-", StringComparison.Ordinal) &&
            !key.StartsWith("ecdsa-", StringComparison.Ordinal))
            return BadRequest(new { error = "Invalid SSH public key format." });

        // Check for duplicate key
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.SshPublicKey == key);
        if (existing != null)
            return BadRequest(new { error = "This SSH key is already registered." });

        var domainUser = Domain.Entities.User.CreateWithSsh(key, request.DisplayName);
        _db.Users.Add(domainUser);
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(domainUser);
        return Ok(new AuthResponse(token, domainUser.Email, domainUser.DisplayName));
    }

    /// <summary>Issue a challenge nonce for the given SSH identity (email or pseudo-email).</summary>
    [HttpPost("challenge")]
    public async Task<IActionResult> Challenge([FromBody] SshChallengeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identity))
            return BadRequest(new { error = "Identity is required." });

        var identity   = request.Identity.ToLowerInvariant();
        var domainUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == identity);

        if (domainUser?.SshPublicKey == null)
            return NotFound(new { error = "No SSH key registered for this identity." });

        var nonce = _challenges.Issue(identity);
        var command = $"printf '%s' '{nonce}' | ssh-keygen -Y sign -f ~/.ssh/id_ed25519 -n agentscope.io";

        return Ok(new SshChallengeResponse(nonce, command));
    }

    /// <summary>Verify a signed challenge, return JWT on success.</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] SshVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identity) || string.IsNullOrWhiteSpace(request.Signature))
            return BadRequest(new { error = "Identity and signature are required." });

        var identity = request.Identity.ToLowerInvariant();

        if (!_challenges.TryConsume(identity, out var nonce))
            return BadRequest(new { error = "Challenge expired or not found. Request a new one." });

        var domainUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == identity);
        if (domainUser?.SshPublicKey == null)
            return Unauthorized(new { error = "No SSH key registered for this identity." });

        var valid = await VerifySshSignatureAsync(domainUser.SshPublicKey, identity, nonce, request.Signature);
        if (!valid)
            return Unauthorized(new { error = "Invalid signature." });

        var token = GenerateJwtToken(domainUser);
        return Ok(new AuthResponse(token, domainUser.Email, domainUser.DisplayName));
    }

    private async Task<bool> VerifySshSignatureAsync(
        string publicKey, string identity, string nonce, string signature)
    {
        var tmpDir          = Path.GetTempPath();
        var allowedSigners  = Path.Combine(tmpDir, $"as_{Guid.NewGuid():N}");
        var signatureFile   = Path.Combine(tmpDir, $"ss_{Guid.NewGuid():N}");

        try
        {
            // allowed_signers format: "{principal} {key_type} {key_data}"
            await System.IO.File.WriteAllTextAsync(allowedSigners, $"{identity} {publicKey}");
            await System.IO.File.WriteAllTextAsync(signatureFile, signature);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("ssh-keygen",
                    $"-Y verify -f {allowedSigners} -I {identity} -n agentscope.io -s {signatureFile}")
                {
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                }
            };

            process.Start();
            await process.StandardInput.WriteAsync(nonce);
            process.StandardInput.Close();

            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                _logger.LogWarning("SSH verify failed for {Identity}: {Stderr}", identity, stderr);

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH verification error for {Identity}", identity);
            return false;
        }
        finally
        {
            try { System.IO.File.Delete(allowedSigners); } catch { }
            try { System.IO.File.Delete(signatureFile); } catch { }
        }
    }

    private string GenerateJwtToken(User domainUser)
    {
        var key   = _config["Jwt:Key"] ?? "SuperSecretKeyForDevelopmentOnly1234567890!";
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,    domainUser.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email,  domainUser.Email),
            new Claim("AgentScope_UserId",            domainUser.Id.ToString()),
            new Claim("AgentScope_Tier",              domainUser.Tier.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
