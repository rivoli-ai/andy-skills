using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Options;
using SkillRegistry.Infrastructure.Auth;
using SkillRegistry.Infrastructure.Persistence;

namespace SkillRegistry.Infrastructure.Services;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.Configure<SkillsStorageOptions>(opts =>
        {
            var sec = configuration.GetSection(SkillsStorageOptions.SectionName);
            var url = sec["PublicBaseUrl"];
            if (!string.IsNullOrWhiteSpace(url))
                opts.PublicBaseUrl = url.Trim();
        });

        services.Configure<AuthProvidersOptions>(opts =>
        {
            var section = configuration.GetSection(AuthProvidersOptions.SectionName);
            opts.Providers = new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in section.GetChildren())
            {
                var pc = new ProviderConfig();
                child.Bind(pc);
                opts.Providers[child.Key] = pc;
            }
        });
        services.AddSingleton<AuthProviderRegistry>();
        services.AddSingleton<RegistryJwtIssuer>();

        services.AddDbContext<SkillRegistryDbContext>(options => options.UseNpgsql(cs));
        services.AddScoped<ISkillRegistryPersistence, SkillRegistryPersistence>();

        services.AddHttpClient<RemoteArtifactFetcher>()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
            })
            .ConfigureHttpClient(static client =>
            {
                client.Timeout = TimeSpan.FromMinutes(10);
            });
        services.AddScoped<IRemoteArtifactFetcher>(sp => sp.GetRequiredService<RemoteArtifactFetcher>());

        return services;
    }
}
