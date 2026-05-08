using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SkillRegistry.Infrastructure.Auth;

/// <summary>Issues HMAC-signed JWTs for API authorization after OIDC validation.</summary>
public sealed class RegistryJwtIssuer(IConfiguration configuration)
{
    public string CreateAccessToken(string subject, string email, string? displayName)
    {
        var secretKey = configuration["JWT:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("JWT:SecretKey must be configured to issue API tokens.");

            secretKey = "skill-registry-dev-secret-min-32-chars!!";
        }

        if (secretKey.Length < 32)
            throw new InvalidOperationException("JWT:SecretKey must be at least 32 characters.");

        var issuer = configuration["JWT:Issuer"] ?? "SkillRegistry";
        var audience = configuration["JWT:Audience"] ?? "SkillRegistry";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Email, email),
        };

        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim(ClaimTypes.Name, displayName));

        var expiresHours = configuration.GetValue("JWT:ExpiryHours", 12);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(expiresHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
