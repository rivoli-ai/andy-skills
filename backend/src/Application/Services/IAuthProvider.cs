using System.Security.Claims;

namespace SkillRegistry.Application.Services;

/// <summary>External OIDC provider — validates frontend-obtained tokens (Azure AD, Duende).</summary>
public interface IAuthProvider
{
    string Name { get; }
    string Type { get; }

    Task<ClaimsPrincipal> ValidateTokenAsync(string jwt, CancellationToken ct);
    Task<ExternalUserProfile> GetUserProfileAsync(string accessToken, CancellationToken ct);
}

public sealed class ExternalUserProfile
{
    public required string ProviderUserId { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
}
