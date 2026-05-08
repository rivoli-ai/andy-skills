using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Options;
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

        services.AddDbContext<SkillRegistryDbContext>(options => options.UseNpgsql(cs));
        services.AddScoped<ISkillRegistryPersistence, SkillRegistryPersistence>();

        return services;
    }
}
