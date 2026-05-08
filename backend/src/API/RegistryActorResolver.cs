using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SkillRegistry.API;

/// <summary>Resolves audit actor: JWT claims when authenticated, else <c>X-Dev-User-Id</c>, else anonymous.</summary>
public static class RegistryActorResolver
{
    public static string Resolve(IHttpContextAccessor httpContextAccessor)
    {
        var http = httpContextAccessor.HttpContext;
        var user = http?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(JwtRegisteredClaimNames.Email);
            if (!string.IsNullOrWhiteSpace(email))
                return email.Trim();

            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!string.IsNullOrWhiteSpace(sub))
                return sub.Trim();
        }

        if (http?.Request.Headers.TryGetValue("X-Dev-User-Id", out var v) == true && !string.IsNullOrWhiteSpace(v))
            return v.ToString().Trim();

        return "anonymous";
    }
}
