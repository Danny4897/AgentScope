using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AgentScope.Api.Controllers;

public record GitHubCallbackRequest(string Code, string? State);

[ApiController]
[Route("api/v1/auth/github")]
public class GitHubAuthController : ControllerBase
{
    private readonly AgentScopeDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubAuthController> _logger;

    public GitHubAuthController(
        AgentScopeDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubAuthController> logger)
    {
        _db                = db;
        _config            = config;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    /// <summary>
    /// Returns the GitHub OAuth authorization URL the frontend should redirect to.
    /// </summary>
    [HttpGet("url")]
    public IActionResult GetAuthUrl()
    {
        var clientId  = _config["GitHub:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(503, new { error = "GitHub OAuth not configured. Set GitHub:ClientId in appsettings." });

        var state    = Guid.NewGuid().ToString("N");
        var redirect = _config["GitHub:RedirectUri"] ?? "http://localhost:7000/api/v1/auth/github/callback";
        var url = $"https://github.com/login/oauth/authorize" +
                  $"?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirect)}" +
                  $"&scope=read:user,user:email,read:public_key&state={state}";

        return Ok(new { url, state });
    }

    /// <summary>
    /// GitHub redirects here after user authorizes. Exchanges code for token,
    /// fetches user info, creates/finds domain user, returns JWT.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state)
    {
        var clientId     = _config["GitHub:ClientId"];
        var clientSecret = _config["GitHub:ClientSecret"];
        var webBaseUrl   = _config["WebBaseUrl"] ?? "http://localhost:7001";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogError("GitHub OAuth not configured");
            return Redirect($"{webBaseUrl}/login?error=github_not_configured");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentScope/1.0");

            // Exchange code for access token
            var tokenResp = await client.PostAsJsonAsync("https://github.com/login/oauth/access_token", new
            {
                client_id     = clientId,
                client_secret = clientSecret,
                code
            });
            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = tokenJson.GetProperty("access_token").GetString();

            if (string.IsNullOrWhiteSpace(accessToken))
                return Redirect($"{webBaseUrl}/login?error=github_token_failed");

            // Fetch GitHub user
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var ghUser = await client.GetFromJsonAsync<JsonElement>("https://api.github.com/user");

            var ghId          = ghUser.GetProperty("id").GetInt64().ToString();
            var ghLogin       = ghUser.GetProperty("login").GetString() ?? "github_user";
            var ghDisplayName = ghUser.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null
                ? n.GetString() ?? ghLogin : ghLogin;

            // Fetch primary email
            var emailsResp = await client.GetFromJsonAsync<JsonElement[]>("https://api.github.com/user/emails") ?? [];
            var primaryEmail = emailsResp
                .Where(e => e.TryGetProperty("primary", out var p) && p.GetBoolean())
                .Select(e => e.GetProperty("email").GetString())
                .FirstOrDefault() ?? $"github_{ghId}@agentscope.local";

            // Fetch SSH keys from GitHub and store the first one
            var keysResp = await client.GetFromJsonAsync<JsonElement[]>("https://api.github.com/user/keys") ?? [];
            var firstKey = keysResp.Select(k => k.GetProperty("key").GetString()).FirstOrDefault();

            // Find or create domain user
            var domainUser = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == ghId)
                          ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == primaryEmail.ToLowerInvariant());

            if (domainUser == null)
            {
                domainUser = Domain.Entities.User.CreateWithGitHub(ghId, primaryEmail, ghDisplayName);
                if (firstKey != null) domainUser.SetSshPublicKey(firstKey);
                _db.Users.Add(domainUser);
            }
            else
            {
                domainUser.SetGitHubId(ghId);
                if (firstKey != null && domainUser.SshPublicKey == null)
                    domainUser.SetSshPublicKey(firstKey);
            }

            await _db.SaveChangesAsync();

            var jwt = GenerateJwtToken(domainUser);

            // Redirect to Web with token in fragment (never in query string)
            return Redirect($"{webBaseUrl}/auth/github?token={Uri.EscapeDataString(jwt)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub OAuth callback error");
            return Redirect($"{webBaseUrl}/login?error=github_error");
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
