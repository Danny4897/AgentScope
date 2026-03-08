using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace AgentScope.Web.Services;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly LocalStorageService _localStorage;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _cachedToken;

    public JwtAuthStateProvider(LocalStorageService localStorage) =>
        _localStorage = localStorage;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync("jwt_token");
            if (string.IsNullOrWhiteSpace(token))
                return Anonymous;

            _cachedToken = token;

            if (IsTokenExpired(token))
            {
                await _localStorage.RemoveItemAsync("jwt_token");
                _cachedToken = null;
                return Anonymous;
            }

            var principal = ParseToken(token);
            return new AuthenticationState(principal);
        }
        catch
        {
            return Anonymous;
        }
    }

    public async Task NotifyLoginAsync(string token)
    {
        _cachedToken = token;
        await _localStorage.SetItemAsync("jwt_token", token);
        var principal = ParseToken(token);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public async Task NotifyLogoutAsync()
    {
        _cachedToken = null;
        await _localStorage.RemoveItemAsync("jwt_token");
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    public string? GetCachedToken() => _cachedToken;

    public async Task<string?> GetTokenAsync()
    {
        if (_cachedToken != null) return _cachedToken;
        var token = await _localStorage.GetItemAsync("jwt_token");
        if (!string.IsNullOrWhiteSpace(token) && !IsTokenExpired(token))
        {
            _cachedToken = token;
            return token;
        }
        return null;
    }

    private static ClaimsPrincipal ParseToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return new ClaimsPrincipal(new ClaimsIdentity());

            var payload = parts[1];
            // Pad base64url
            payload = payload.Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

            var claims = new List<Claim>();
            if (data.TryGetValue("sub", out var sub)) claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.GetString() ?? ""));
            if (data.TryGetValue("email", out var email)) claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? ""));
            if (data.TryGetValue("AgentScope_UserId", out var uid)) claims.Add(new Claim("AgentScope_UserId", uid.GetString() ?? ""));
            if (data.TryGetValue("AgentScope_Tier", out var tier)) claims.Add(new Claim("AgentScope_Tier", tier.GetString() ?? ""));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

            if (data.TryGetValue("exp", out var expEl))
            {
                var exp = expEl.GetInt64();
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }
}
