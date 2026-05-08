namespace SkillRegistry.Application.Options;

/// <summary>Strongly-typed <c>AuthProviders</c> section (DevPilot-compatible).</summary>
public sealed class AuthProvidersOptions
{
    public const string SectionName = "AuthProviders";

    public Dictionary<string, ProviderConfig> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<KeyValuePair<string, ProviderConfig>> GetEnabledProviders()
        => Providers.Where(p => p.Value.Enabled);
}

/// <summary>Configuration for one auth provider (Azure AD, Duende, etc.).</summary>
public sealed class ProviderConfig
{
    public bool Enabled { get; set; }

    /// <summary><c>FrontendOidc</c> for SPA code flow + backend token validation.</summary>
    public string Type { get; set; } = "FrontendOidc";

    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? SpaClientId { get; set; }
    public string? TenantId { get; set; }
    public string? Scopes { get; set; }
    public string? ProfileEndpoint { get; set; }

    /// <summary>Ignored at runtime; logged if true (DevPilot parity).</summary>
    public bool DangerousAcceptAnyServerCertificate { get; set; }
}
