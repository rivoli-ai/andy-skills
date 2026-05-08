using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillRegistry.Application.Options;
using SkillRegistry.Application.Services;

namespace SkillRegistry.Infrastructure.Auth;

/// <summary>Registers enabled <see cref="IAuthProvider"/> instances from configuration.</summary>
public sealed class AuthProviderRegistry
{
    private readonly Dictionary<string, IAuthProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public AuthProviderRegistry(
        IOptions<AuthProvidersOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthProviderRegistry> logger)
    {
        Options = options.Value;

        foreach (var (name, config) in Options.Providers)
        {
            if (!config.Enabled)
            {
                logger.LogInformation("Auth provider '{Provider}' is disabled.", name);
                continue;
            }

            if (config.DangerousAcceptAnyServerCertificate)
            {
                logger.LogWarning(
                    "Auth provider '{Provider}': DangerousAcceptAnyServerCertificate is ignored. Use http:// authority or trust HTTPS dev certs.",
                    name);
            }

            var type = config.Type?.Trim().ToLowerInvariant() ?? "frontendoidc";
            if (type != "frontendoidc")
            {
                logger.LogWarning(
                    "Auth provider '{Provider}': type '{Type}' is not supported (only FrontendOidc).",
                    name,
                    config.Type);
                continue;
            }

            _providers[name] = new OidcAuthProvider(name, config, httpClientFactory);
            logger.LogInformation("Auth provider '{Provider}' (FrontendOidc) registered.", name);
        }
    }

    public AuthProvidersOptions Options { get; }

    public bool TryGetProvider(string name, out IAuthProvider? provider) =>
        _providers.TryGetValue(name, out provider);
}
