using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.Infrastructure.Auth;

namespace SkillRegistry.API.Controllers;

/// <summary>OIDC configuration for SPA + exchange external tokens for Skill Registry JWT.</summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public sealed class AuthController(AuthProviderRegistry providerRegistry, RegistryJwtIssuer jwtIssuer) : ControllerBase
{
    /// <summary>Frontend-safe provider metadata (mirrors DevPilot <c>GET /api/auth/config</c>).</summary>
    [HttpGet("config")]
    public IActionResult GetAuthConfig()
    {
        var providers = new List<object>();

        foreach (var (name, config) in providerRegistry.Options.GetEnabledProviders())
        {
            var type = string.IsNullOrWhiteSpace(config.Type) ? "FrontendOidc" : config.Type;
            if (!string.Equals(type, "FrontendOidc", StringComparison.OrdinalIgnoreCase))
                continue;

            var frontendClientId = !string.IsNullOrEmpty(config.SpaClientId) ? config.SpaClientId : config.ClientId;

            providers.Add(new
            {
                name,
                type = "FrontendOidc",
                authority = config.Authority,
                clientId = frontendClientId,
                scopes = config.Scopes,
                tenantId = config.TenantId,
            });
        }

        return Ok(new { providers });
    }

    public sealed record TokenRequest(string? IdToken, string? AccessToken);

    public sealed record AuthUserDto(string Id, string Email, string? Name);

    public sealed record AuthResponse(string Token, AuthUserDto User);

    /// <summary>Validate Azure AD / Duende token from SPA and return Skill Registry JWT.</summary>
    [HttpPost("{provider}/token")]
    public async Task<IActionResult> ExchangeOidcToken(string provider, [FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        if (!providerRegistry.TryGetProvider(provider, out var authProvider) || authProvider is null)
            return NotFound(new { message = $"Provider '{provider}' is not enabled." });

        var tokenToValidate = !string.IsNullOrWhiteSpace(request.IdToken) ? request.IdToken : request.AccessToken;
        if (string.IsNullOrWhiteSpace(tokenToValidate))
            return BadRequest(new { message = "idToken or accessToken is required." });

        try
        {
            await authProvider.ValidateTokenAsync(tokenToValidate, cancellationToken).ConfigureAwait(false);

            var profileToken = !string.IsNullOrWhiteSpace(request.AccessToken) ? request.AccessToken : tokenToValidate;
            var profile = await authProvider.GetUserProfileAsync(profileToken, cancellationToken).ConfigureAwait(false);

            var email = profile.Email?.Trim();
            if (string.IsNullOrEmpty(email))
                email = profile.ProviderUserId?.Trim();

            if (string.IsNullOrEmpty(email))
                return BadRequest(new { message = "Could not resolve user email or id from identity provider." });

            var subject = !string.IsNullOrWhiteSpace(profile.ProviderUserId) ? profile.ProviderUserId.Trim() : email;
            var token = jwtIssuer.CreateAccessToken(subject, email, profile.DisplayName?.Trim());

            return Ok(new AuthResponse(
                token,
                new AuthUserDto(subject, email, profile.DisplayName?.Trim())));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Invalid or expired token: {ex.Message}" });
        }
    }
}
