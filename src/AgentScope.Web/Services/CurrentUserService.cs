using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AgentScope.Web.Services;

public sealed class CurrentUserService
{
    private readonly AuthenticationStateProvider _authProvider;

    public CurrentUserService(AuthenticationStateProvider authProvider) =>
        _authProvider = authProvider;

    public async Task<Guid?> GetUserIdAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirst("AgentScope_UserId");
        if (claim == null || !Guid.TryParse(claim.Value, out var id)) return null;
        return id;
    }

    public async Task<string?> GetEmailAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (_authProvider is JwtAuthStateProvider jwtProvider)
            return await jwtProvider.GetTokenAsync();
        return null;
    }
}
